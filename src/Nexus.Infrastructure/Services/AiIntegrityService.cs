using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nexus.Application.DTOs;
using Nexus.Application.Interfaces;
using Nexus.Domain.Entities;
using Nexus.Infrastructure.Data;
using Nexus.Infrastructure.Options;

namespace Nexus.Infrastructure.Services;

public class AiIntegrityService : IAIIntegrityService
{
    private static readonly Uri Endpoint = new("https://api.openai.com/v1/chat/completions");
    private readonly HttpClient _httpClient;
    private readonly NexusDbContext _dbContext;
    private readonly OpenAiOptions _options;
    private readonly ILogger<AiIntegrityService> _logger;

    public AiIntegrityService(
        HttpClient httpClient,
        NexusDbContext dbContext,
        IOptions<OpenAiOptions> options,
        ILogger<AiIntegrityService> logger)
    {
        _httpClient = httpClient;
        _dbContext = dbContext;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<RawCarrierRecord?> TryMapUnknownAsync(RawCarrierRecord record, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey) || _options.ApiKey.StartsWith("your-", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("OpenAI API key not configured. Skipping smart mapping.");
            return null;
        }

        var sourceHeaders = record.Fields.Keys.ToList();
        if (sourceHeaders.Count == 0) return null;

        _logger.LogInformation("Calling OpenAI to map {Count} headers for carrier {CarrierCode}: {Headers}", 
            sourceHeaders.Count, record.CarrierCode, string.Join(", ", sourceHeaders));

        var canonicalFields = await _dbContext.CanonicalFields
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.SortOrder)
            .ToListAsync(cancellationToken);

        var canonicalList = canonicalFields.Count > 0
            ? string.Join("\n", canonicalFields.Select(field =>
                $"- {field.FieldName} : {field.Description}" +
                (string.IsNullOrWhiteSpace(field.Examples) ? string.Empty : $" (examples: {field.Examples})")))
            : string.Join("\n", new[]
            {
                "- ExternalId : A unique identifier for the transaction",
                "- PolicyNumber : The insurance policy number",
                "- GrossPremium: The gross premium amount",
                "- NetCommission: The net commission amount",
                "- TransactionDate: The exact date of the transaction",
                "- Notes: Any additional notes or status text"
            });

        var prompt = $$"""
You are an expert data integration assistant for an insurance agency.
Your task is to map an unknown CSV/Excel file's headers to our internal canonical database fields.

Our internal canonical fields are:
{{canonicalList}}

The incoming file from carrier '{{record.CarrierCode}}' has these exact headers:
[{{string.Join(", ", sourceHeaders.Select(h => $"\"{h}\""))}}]

Please map the incoming headers to our internal fields. 
Be creative - for example "comm_amount", "amount", "net", "comision" could mean NetCommission. "date_effective", "eff_dt", "fecha" could mean TransactionDate. "pol_no", "poliza" could mean PolicyNumber.
Not all incoming headers need to be mapped, but try to find the best match for our core fields. Account for english, spanish, or common abbreviations.

Respond ONLY with a valid JSON array of objects. Each object must have "sourceField" (the exact incoming header) and "targetField" (our exact canonical field name). Do not include markdown code blocks.

Example output format:
[
  { "sourceField": "policy_no", "targetField": "PolicyNumber" },
  { "sourceField": "comm_amt", "targetField": "NetCommission" },
  { "sourceField": "eff_date", "targetField": "TransactionDate" }
]
""";

        var requestBody = new
        {
            model = _options.Model ?? "gpt-4o-mini",
            messages = new[]
            {
                new { role = "system", content = "You are a helpful data mapping parsing assistant. You output raw JSON arrays." },
                new { role = "user", content = prompt }
            },
            temperature = 0.1
        };

        var requestMsg = new HttpRequestMessage(HttpMethod.Post, Endpoint)
        {
            Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json")
        };
        requestMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);

        try
        {
            var response = await _httpClient.SendAsync(requestMsg, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("OpenAI API error: {Status} - {Error}", response.StatusCode, err);
                return null;
            }

            var jsonResponse = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(jsonResponse);
            var contentString = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            if (contentString == null) return null;

            contentString = contentString.Trim();
            if (contentString.StartsWith("```json"))
            {
                contentString = contentString.Substring(7);
                if (contentString.EndsWith("```")) contentString = contentString.Substring(0, contentString.Length - 3);
            }
            contentString = contentString.Trim();

            var mappings = JsonSerializer.Deserialize<List<AiMappingResult>>(contentString, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            
            if (mappings != null && mappings.Any())
            {
                _logger.LogInformation("OpenAI successfully mapped {Count} fields for carrier {CarrierCode}", mappings.Count, record.CarrierCode);
                
                var dbMappings = new List<CarrierMapping>();
                foreach (var map in mappings)
                {
                    if (string.IsNullOrWhiteSpace(map.SourceField) || string.IsNullOrWhiteSpace(map.TargetField)) continue;
                    
                    dbMappings.Add(new CarrierMapping
                    {
                        Id = Guid.NewGuid(),
                        CarrierCode = record.CarrierCode,
                        SourceField = map.SourceField,
                        TargetField = map.TargetField,
                        IsRequired = map.TargetField.Equals("PolicyNumber", StringComparison.OrdinalIgnoreCase) || 
                                     map.TargetField.Equals("ExternalId", StringComparison.OrdinalIgnoreCase),
                        CreatedAt = DateTime.UtcNow
                    });
                }

                if (dbMappings.Any())
                {
                    await _dbContext.CarrierMappings.AddRangeAsync(dbMappings, cancellationToken);
                    await _dbContext.SaveChangesAsync(cancellationToken);
                    return record;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to perform AI smart mapping for carrier {CarrierCode}", record.CarrierCode);
        }

        return null;
    }

    private class AiMappingResult
    {
        public string SourceField { get; set; } = string.Empty;
        public string TargetField { get; set; } = string.Empty;
    }
}
