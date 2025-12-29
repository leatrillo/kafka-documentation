namespace Coelsa.Artifact.Kafka.Model.SqlServer;
internal class OutboxMessages
{
    public required string Id { get; init; }
    public required string Topic { get; init; }
    public required string Payload { get; init; }
    public required string SpecVersion { get; init; }
    public required string Source { get; init; }
    public required string Type { get; init; }
    public required DateTimeOffset Time { get; init; }
    public required string DataContentType { get; init; }
    public required string TraceId { get; init; }
    public DateTimeOffset? CreatedAt { get; init; } = null;
    public DateTimeOffset? ProcessedAt { get; init; } = null;
    public int RetryCount { get; init; } = 0;
    public DateTimeOffset? NextRetryAt { get; init; } = null;
    public string? Error { get; init; } = null;
    public int Status { get; init; } = 0;

}
