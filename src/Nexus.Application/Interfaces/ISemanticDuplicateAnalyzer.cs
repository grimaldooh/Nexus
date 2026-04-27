using Nexus.Domain.Entities;

namespace Nexus.Application.Interfaces;

public interface ISemanticDuplicateAnalyzer
{
    Task<SemanticAnalysisResult> AnalyzeAsync(InsuranceTransaction transaction, CancellationToken cancellationToken);
}

public record SemanticAnalysisResult(bool IsSuspect, decimal? ConfidenceScore, string? Reason);
