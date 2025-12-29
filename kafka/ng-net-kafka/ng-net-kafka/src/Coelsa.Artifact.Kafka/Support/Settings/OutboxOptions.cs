namespace Coelsa.Artifact.Kafka.Support.Settings;

internal sealed class OutboxOptions
{
    /// <summary>
    /// Gets or sets the maximum number of concurrent processors.
    /// Default is 1.
    /// </summary>
    public int MaxConcurrency { get; set; } = 1;
}
