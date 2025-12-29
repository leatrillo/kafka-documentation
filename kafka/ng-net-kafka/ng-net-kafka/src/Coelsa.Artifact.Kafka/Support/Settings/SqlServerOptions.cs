namespace Coelsa.Artifact.Kafka.Support.Settings;
internal sealed class SqlServerOptions
{
    public string Initials { get; set; } = "ARQ";

    public int Retries { get; init; } = 3;

    /// <summary>
    /// Tiene un limite máximo de 30 segundos. Se va incrementando exponencialmente.
    /// </summary>
    public int RetryIntervalSeconds { get; init; } = 2;
}
