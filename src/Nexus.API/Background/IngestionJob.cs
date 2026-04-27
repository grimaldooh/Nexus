namespace Nexus.API.Background;

public record IngestionJob(Guid BatchId, string FilePath, string SourceName);
