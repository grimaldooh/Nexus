using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nexus.Application.Interfaces;
using Nexus.Domain.Entities;
using Nexus.Infrastructure.Options;

namespace Nexus.Infrastructure.Services;

public class OpenAiSemanticDuplicateAnalyzer : ISemanticDuplicateAnalyzer
{
    private static readonly Uri Endpoint = new("https://api.openai.com/v1/responses");
    private readonly HttpClient _httpClient;
    private readonly OpenAiOptions _options;
    private readonly ILogger<OpenAiSemanticDuplicateAnalyzer> _logger;

    public OpenAiSemanticDuplicateAnalyzer(
        HttpClient httpClient,
        IOptions<OpenAiOptions> options,
        ILogger<OpenAiSemanticDuplicateAnalyzer> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<SemanticAnalysisResult> AnalyzeAsync(InsuranceTransaction transaction, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            return new SemanticAnalysisResult(false, null, "OpenAI API key not configured.");
        }

        var prompt = $"Assess if the following insurance transaction appears duplicate or cancelled. " +
                     $"Return JSON with keys: isSuspect (bool), confidence (0-1), reason (string). " +
                     $"PolicyNumber: {transaction.PolicyNumber}; Amount: {transaction.Amount}; " +
                     $"Date: {transaction.TransactionDate:O}; Notes: {transaction.Notes}";

        using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _options.ApiKey);
        request.Content = JsonContent.Create(new
        {
            model = _options.Model,
            input = prompt
        });

        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(contentStream, cancellationToken: cancellationToken);

            var outputText = ExtractOutputText(document.RootElement);
            if (string.IsNullOrWhiteSpace(outputText))
            {
                return new SemanticAnalysisResult(false, null, null);
            }

            var parsed = TryParseResult(outputText);
            if (parsed is not null)
            {
                return parsed;
            }

            var isSuspect = outputText.Contains("suspect", StringComparison.OrdinalIgnoreCase) &&
                            outputText.Contains("true", StringComparison.OrdinalIgnoreCase);
            return new SemanticAnalysisResult(isSuspect, null, outputText.Trim());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OpenAI analysis failed for transaction {TransactionId}.", transaction.Id);
            return new SemanticAnalysisResult(false, null, "OpenAI analysis failed.");
        }
    }

    private static string? ExtractOutputText(JsonElement root)
    {
        if (root.TryGetProperty("output_text", out var outputText))
        {
            return outputText.GetString();
        }

        if (!root.TryGetProperty("output", out var outputArray))
        {
            return null;
        }

        foreach (var item in outputArray.EnumerateArray())
        {
            if (!item.TryGetProperty("content", out var contentArray))
            {
                continue;
            }

            foreach (var content in contentArray.EnumerateArray())
            {
                if (content.TryGetProperty("text", out var textElement))
                {
                    return textElement.GetString();
                }
            }
        }

        return null;
    }

    private static SemanticAnalysisResult? TryParseResult(string outputText)
    {
        outputText = outputText.Trim();
        if (!outputText.StartsWith("{", StringComparison.Ordinal))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(outputText);
            var root = document.RootElement;
            var isSuspect = root.GetProperty("isSuspect").GetBoolean();
            var confidence = root.TryGetProperty("confidence", out var confElement)
                ? confElement.GetDecimal()
                : (decimal?)null;
            var reason = root.TryGetProperty("reason", out var reasonElement)
                ? reasonElement.GetString()
                : null;

            return new SemanticAnalysisResult(isSuspect, confidence, reason);
        }
        catch
        {
            return null;
        }
    }
}
