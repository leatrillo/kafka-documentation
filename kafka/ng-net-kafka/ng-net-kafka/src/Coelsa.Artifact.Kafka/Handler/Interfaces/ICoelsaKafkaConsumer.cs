using Coelsa.Artifact.Kafka.Model.Enum;

namespace Coelsa.Artifact.Kafka.Handler.Interfaces;
public interface ICoelsaKafkaConsumer
{
    IConsumer<string, string> Subscribe(string topic, ConsumerType consumerType = ConsumerType.queue_consumer);

    IConsumer<string, string> Subscribe(List<string> topics, ConsumerType consumerType = ConsumerType.queue_consumer);

    Task<bool> ConsumeAsync<T>(IConsumer<string, string> consumer, Func<T?, CancellationToken, Task<bool>> messageHandler, CancellationToken cancellationToken = default) where T : class;

    bool Consume<T>(IConsumer<string, string> consumer, Func<T?, CancellationToken, bool> messageHandler, CancellationToken cancellationToken = default) where T : class;
}
