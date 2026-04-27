using Nexus.Domain.Enums;

namespace Nexus.Domain.Entities;

public class Agent
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public AgentRole Role { get; set; }
    public Guid? ReportsToId { get; set; }

    public Agent? ReportsTo { get; set; }
    public List<Agent> DirectReports { get; set; } = new();
}
