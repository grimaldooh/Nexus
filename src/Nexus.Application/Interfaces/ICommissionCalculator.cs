using Nexus.Domain.Entities;

namespace Nexus.Application.Interfaces;

public interface ICommissionCalculator
{
    Task<IReadOnlyList<CommissionSplit>> CalculateSplitsAsync(InsuranceTransaction transaction, CancellationToken cancellationToken);
}

public record CommissionSplit(Guid AgentId, decimal Amount);
