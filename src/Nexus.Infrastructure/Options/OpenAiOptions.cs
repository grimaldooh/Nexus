namespace Nexus.Infrastructure.Options;

public class OpenAiOptions
{
    public const string SectionName = "OpenAi";

    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "gpt-4o-mini";
    public double SuspectThreshold { get; set; } = 0.7;
}
