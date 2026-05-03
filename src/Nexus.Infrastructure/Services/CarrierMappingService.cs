using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nexus.Application.DTOs;
using Nexus.Application.Interfaces;
using Nexus.Domain.Entities;
using Nexus.Domain.Enums;
using Nexus.Infrastructure.Data;

namespace Nexus.Infrastructure.Services;

public class CarrierMappingService : ICarrierMappingService
{
    private readonly NexusDbContext _dbContext;
    private readonly IAIIntegrityService _aiIntegrityService;
    private readonly ILogger<CarrierMappingService> _logger;

    public CarrierMappingService(
        NexusDbContext dbContext,
        IAIIntegrityService aiIntegrityService,
        ILogger<CarrierMappingService> logger)
    {
        _dbContext = dbContext;
        _aiIntegrityService = aiIntegrityService;
        _logger = logger;
    }

    public async Task<InsuranceTransaction> MapAsync(Guid batchId, RawCarrierRecord record, CancellationToken cancellationToken)
    {
        var mappings = await _dbContext.CarrierMappings
            .AsNoTracking()
            .Where(x => x.CarrierCode == record.CarrierCode)
            .ToListAsync(cancellationToken);

        if (mappings.Count == 0)
        {
            var aiRecord = await _aiIntegrityService.TryMapUnknownAsync(record, cancellationToken);
            if (aiRecord is not null)
            {
                record = aiRecord;
                mappings = await _dbContext.CarrierMappings
                    .AsNoTracking()
                    .Where(x => x.CarrierCode == record.CarrierCode)
                    .ToListAsync(cancellationToken);
            }
        }

        var transaction = new InsuranceTransaction
        {
            Id = Guid.NewGuid(),
            BatchId = batchId,
            CarrierCode = record.CarrierCode,
            Status = TransactionStatus.Pending,
            TransactionDate = DateTime.UtcNow
        };

        if (mappings.Count == 0)
        {
            transaction.Status = TransactionStatus.Invalid;
            _logger.LogWarning("No carrier mappings found for carrier {CarrierCode}.", record.CarrierCode);
            return transaction;
        }

        var missingRequired = false;

        foreach (var mapping in mappings)
        {
            record.Fields.TryGetValue(mapping.SourceField, out var rawValue);

            if (mapping.IsRequired && string.IsNullOrWhiteSpace(rawValue))
            {
                missingRequired = true;
                continue;
            }

            ApplyMapping(transaction, mapping.TargetField, rawValue);
        }

        if (missingRequired || string.IsNullOrWhiteSpace(transaction.ExternalId) || string.IsNullOrWhiteSpace(transaction.PolicyNumber))
        {
            transaction.Status = TransactionStatus.Invalid;
        }

        return transaction;
    }

    private static void ApplyMapping(InsuranceTransaction transaction, string targetField, string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(targetField))
        {
            return;
        }

        var normalized = targetField.Trim();
        switch (normalized.ToLowerInvariant())
        {
            case "externalid":
                transaction.ExternalId = rawValue ?? string.Empty;
                break;
            case "policynumber":
                transaction.PolicyNumber = rawValue ?? string.Empty;
                break;
            case "grosspremium":
                if (TryGetDecimal(rawValue, out var grossPremium))
                {
                    transaction.GrossPremium = grossPremium;
                }
                break;
            case "netcommission":
                if (TryGetDecimal(rawValue, out var netCommission))
                {
                    transaction.NetCommission = netCommission;
                }
                break;
            case "carriercode":
                if (!string.IsNullOrWhiteSpace(rawValue))
                {
                    transaction.CarrierCode = rawValue;
                }
                break;
            case "transactiondate":
                if (DateTime.TryParse(rawValue, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var date))
                {
                    transaction.TransactionDate = date.ToUniversalTime();
                }
                break;
            case "notes":
                transaction.Notes = rawValue;
                break;
        }
    }

    private static bool TryGetDecimal(string? rawValue, out decimal value)
    {
        return decimal.TryParse(rawValue, NumberStyles.Any, CultureInfo.InvariantCulture, out value);
    }
}
