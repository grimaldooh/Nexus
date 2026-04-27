using Nexus.Application.Interfaces;
using Nexus.Domain.Entities;

namespace Nexus.Infrastructure.Services;

public class CommissionCalculator : ICommissionCalculator
{
    public Task<IReadOnlyList<CommissionSplit>> CalculateSplitsAsync(InsuranceTransaction transaction, CancellationToken cancellationToken)
    {
        var splits = Array.Empty<CommissionSplit>();
        return Task.FromResult<IReadOnlyList<CommissionSplit>>(splits);
    }
}
