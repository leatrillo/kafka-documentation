using Coelsa.Artifact.Kafka.Handler.Interfaces;
using Coelsa.Artifact.Kafka.Model.Enum;
using Coelsa.Artifact.Kafka.Support;

namespace Coelsa.Artifact.Kafka.Handler;
internal class KafkaConsumerHandler(IServiceProvider serviceProvider) : ICoelsaKafkaConsumer
{
    public bool Consume<T>(IConsumer<string, string> consumer, Func<T?, CancellationToken, bool> messageHandler, CancellationToken cancellationToken = default) where T : class
    {
        ConsumeResult<string, string>? consumeResult = consumer.Consume(cancellationToken);

        if (consumeResult is null)
            return false;

        T? message = CoelsaKafkaTools.JsonDeserialize<T>(consumeResult.Message.Value);

        bool response = messageHandler(message, cancellationToken);

        if (response)
            consumer.Commit(consumeResult);

        return response;
    }

    public async Task<bool> ConsumeAsync<T>(IConsumer<string, string> consumer, Func<T?, CancellationToken, Task<bool>> messageHandler, CancellationToken cancellationToken = default) where T : class
    {
        ConsumeResult<string, string> consumeResult = consumer.Consume(cancellationToken);

        T? message = CoelsaKafkaTools.JsonDeserialize<T>(consumeResult.Message.Value);

        bool response = await messageHandler(message, cancellationToken);

        if (response)
            consumer.Commit(consumeResult);

        return response;
    }

    /// <summary>
    /// Suscribirse en un topico
    /// </summary>
    /// <param name="topic"></param>
    /// <param name="consumerType"></param>
    /// <returns></returns>
    public IConsumer<string, string> Subscribe(string topic, ConsumerType consumerType)
    {
        IConsumer<string, string> consumer = serviceProvider.GetRequiredKeyedService<IConsumer<string, string>>(consumerType.ToString());

        consumer.Subscribe(topic);

        return consumer;
    }

    /// <summary>
    /// Subscribirse en varios topicos
    /// </summary>
    /// <param name="topics"></param>
    /// <param name="consumerType"></param>
    /// <returns></returns>
    public IConsumer<string, string> Subscribe(List<string> topics, ConsumerType consumerType)
    {
        IConsumer<string, string> consumer = serviceProvider.GetRequiredKeyedService<IConsumer<string, string>>(consumerType.ToString());

        consumer.Subscribe(topics);

        return consumer;
    }
}
