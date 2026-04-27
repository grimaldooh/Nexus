using Nexus.Domain.Enums;

namespace Nexus.Domain.Entities;

public class SanitizationLog
{
    public Guid Id { get; set; }
    public Guid TransactionId { get; set; }
    public SanitizationDecisionType DecisionType { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public InsuranceTransaction? Transaction { get; set; }
}
