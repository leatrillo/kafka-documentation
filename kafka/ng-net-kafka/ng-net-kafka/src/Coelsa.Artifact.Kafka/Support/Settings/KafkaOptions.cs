namespace Coelsa.Artifact.Kafka.Support.Settings;

internal sealed class KafkaOptions
{
    public ProducerOptions QueuesProducer { get; init; } = new();

    public ConsumerOptions QueuesConsumer { get; init; } = new();

    public ProducerOptions EventsProducer { get; init; } = new();

    public ConsumerOptions EventsConsumer { get; init; } = new();

    public SecurityOptions Security { get; init; } = new();

    public RegistryOptions SchemaRegistry { get; init; } = new();

    public SqlServerOptions SqlServer { get; init; } = new();
    public OutboxOptions Outbox { get; init; } = new();
}
