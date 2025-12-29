namespace Coelsa.Artifact.Kafka.Support.Settings;
internal sealed class RegistryOptions
{
    /// <summary>
    /// Gets or sets whether to use Avro serialization with Schema Registry.
    /// Default is false (use JSON serialization).
    /// </summary>
    public bool UseAvro { get; init; } = false;

    /// <summary>
    /// Gets or sets the Schema Registry URL.
    /// Example: "http://localhost:8081"
    /// </summary>
    public string Url { get; init; } = "https://kafka-edge-tc01.test.coelsaprod.local:7790";

    /// <summary>
    /// Gets or sets the timeout for Schema Registry requests.
    /// Default is 30 seconds.
    /// </summary>
    public TimeSpan RequestTimeout { get; init; } = TimeSpan.FromSeconds(30);

}
