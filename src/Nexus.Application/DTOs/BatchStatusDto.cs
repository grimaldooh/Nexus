using Nexus.Domain.Enums;

namespace Nexus.Application.DTOs;

public class BatchStatusDto
{
    public Guid BatchId { get; set; }
    public BatchStatus Status { get; set; }
    public int TotalRecords { get; set; }
    public int CleanCount { get; set; }
    public int DuplicateCount { get; set; }
    public int SuspectCount { get; set; }
}
