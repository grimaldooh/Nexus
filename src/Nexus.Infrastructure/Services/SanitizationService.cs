using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nexus.Application.Interfaces;
using Nexus.Domain.Entities;
using Nexus.Domain.Enums;
using Nexus.Infrastructure.Data;

namespace Nexus.Infrastructure.Services;

public class SanitizationService : ISanitizationService
{
    private readonly NexusDbContext _dbContext;
    private readonly ISemanticDuplicateAnalyzer _semanticAnalyzer;
    private readonly ILogger<SanitizationService> _logger;

    public SanitizationService(
        NexusDbContext dbContext,
        ISemanticDuplicateAnalyzer semanticAnalyzer,
        ILogger<SanitizationService> logger)
    {
        _dbContext = dbContext;
        _semanticAnalyzer = semanticAnalyzer;
        _logger = logger;
    }

    public async Task RunAsync(Guid batchId, CancellationToken cancellationToken)
    {
        await MarkExactDuplicatesAsync(batchId, cancellationToken);
        await ApplyHeuristicsAsync(batchId, cancellationToken);
        await ApplyAiAnalysisAsync(batchId, cancellationToken);
    }

    private async Task MarkExactDuplicatesAsync(Guid batchId, CancellationToken cancellationToken)
    {
        var duplicateStatus = (int)TransactionStatus.Duplicate;
        var cleanStatus = (int)TransactionStatus.Clean;

        FormattableString sql = $@"
    WITH Ranked AS
    (
        SELECT Id,
           ROW_NUMBER() OVER (PARTITION BY PolicyNumber, Amount, TransactionDate ORDER BY Id) AS rn
        FROM InsuranceTransactions
        WHERE BatchId = {batchId}
    )
    UPDATE t
    SET Status = CASE WHEN r.rn > 1 THEN {duplicateStatus} ELSE {cleanStatus} END
    FROM InsuranceTransactions t
    INNER JOIN Ranked r ON t.Id = r.Id
    WHERE t.BatchId = {batchId};";

        await _dbContext.Database.ExecuteSqlInterpolatedAsync(sql, cancellationToken);
    }

    private async Task ApplyHeuristicsAsync(Guid batchId, CancellationToken cancellationToken)
    {
        var candidates = await _dbContext.InsuranceTransactions
            .Where(x => x.BatchId == batchId && x.Status == TransactionStatus.Clean)
            .ToListAsync(cancellationToken);

        foreach (var transaction in candidates)
        {
            if (transaction.GrossPremium.HasValue && transaction.Amount > transaction.GrossPremium.Value)
            {
                transaction.Status = TransactionStatus.Suspect;
                _dbContext.SanitizationLogs.Add(new SanitizationLog
                {
                    Id = Guid.NewGuid(),
                    TransactionId = transaction.Id,
                    DecisionType = SanitizationDecisionType.Auto,
                    Reason = "Commission exceeds gross premium.",
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task ApplyAiAnalysisAsync(Guid batchId, CancellationToken cancellationToken)
    {
        var candidates = await _dbContext.InsuranceTransactions
            .Where(x => x.BatchId == batchId && x.Status == TransactionStatus.Clean && x.Notes != null)
            .ToListAsync(cancellationToken);

        foreach (var transaction in candidates)
        {
            var analysis = await _semanticAnalyzer.AnalyzeAsync(transaction, cancellationToken);
            if (!analysis.IsSuspect)
            {
                continue;
            }

            transaction.Status = TransactionStatus.Suspect;
            transaction.ConfidenceScore = analysis.ConfidenceScore;

            _dbContext.SanitizationLogs.Add(new SanitizationLog
            {
                Id = Guid.NewGuid(),
                TransactionId = transaction.Id,
                DecisionType = SanitizationDecisionType.Ai,
                Reason = analysis.Reason ?? "AI flagged as suspect.",
                CreatedAt = DateTime.UtcNow
            });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
