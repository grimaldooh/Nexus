using Nexus.Domain.Enums;

namespace Nexus.Domain.Entities;

public class InsuranceTransaction
{
    public Guid Id { get; set; }
    public Guid BatchId { get; set; }
    public string ExternalId { get; set; } = string.Empty;
    public string PolicyNumber { get; set; } = string.Empty;
    public decimal? GrossPremium { get; set; }
    public decimal NetCommission { get; set; }
    public string CarrierCode { get; set; } = string.Empty;
    public DateTime TransactionDate { get; set; }
    public TransactionStatus Status { get; set; }
    public decimal? ConfidenceScore { get; set; }
    public bool PIIMasked { get; set; }
    public string? Notes { get; set; }

    public Batch? Batch { get; set; }
    public List<SanitizationLog> SanitizationLogs { get; set; } = new();
}
