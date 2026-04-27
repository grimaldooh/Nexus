using Nexus.Domain.Enums;

namespace Nexus.Domain.Entities;

public class Batch
{
    public Guid Id { get; set; }
    public string SourceName { get; set; } = string.Empty;
    public DateTime UploadDate { get; set; }
    public BatchStatus Status { get; set; }

    public List<InsuranceTransaction> Transactions { get; set; } = new();
}
