namespace Nexus.Application.DTOs;

public class AuditSuspectDto
{
    public Guid TransactionId { get; set; }
    public string PolicyNumber { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime TransactionDate { get; set; }
    public decimal? ConfidenceScore { get; set; }
    public string? Notes { get; set; }
    public string? Reason { get; set; }
}
