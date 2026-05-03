namespace Nexus.Domain.Entities;

public class CarrierMapping
{
    public Guid Id { get; set; }
    public string CarrierCode { get; set; } = string.Empty;
    public string SourceField { get; set; } = string.Empty;
    public string TargetField { get; set; } = string.Empty;
    public string? TransformRule { get; set; }
    public bool IsRequired { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
