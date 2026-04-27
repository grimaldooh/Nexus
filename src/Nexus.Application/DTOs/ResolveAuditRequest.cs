namespace Nexus.Application.DTOs;

public class ResolveAuditRequest
{
    public Guid TransactionId { get; set; }
    public bool Approve { get; set; }
    public string Reason { get; set; } = string.Empty;
}
