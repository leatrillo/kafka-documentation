using Coelsa.Artifact.Kafka.Handler.Interfaces;
using Coelsa.Artifact.Kafka.Model;
using Coelsa.Artifact.Kafka.Model.Enum;
using Coelsa.Artifact.Kafka.Model.SqlServer;
using Coelsa.Artifact.Kafka.Support;
using Coelsa.Artifact.Kafka.Support.Settings;
using Microsoft.Extensions.Options;
using System.Data;
using System.Diagnostics;
using System.Text;

namespace Coelsa.Artifact.Kafka.Handler;

internal class KafkaProducerHandler(IServiceProvider serviceProvider, IOptions<KafkaOptions> options) : ICoelsaKafkaProducer
{
    public async Task<CoelsaPersistenceStatus> PublishAsync<T>(MessageConfiguration configuration, CoelsaMessage<T> message, CancellationToken cancellationToken = default) where T : class
    {
        string contentType = "application/json";

        string serializedMessage = "";

        if (message.DataContentType == CoelsaDataContentType.Json)
            serializedMessage = CoelsaKafkaTools.JsonSerialize(message.Data);

        string traceId = Activity.Current?.TraceId.ToString() ?? string.Empty;

        Message<string, string> kafkaMessage = new()
        {
            Key = message.Id,
            Value = serializedMessage,
            Headers = [
                new("spec-version", Encoding.UTF8.GetBytes(message.SpecVersion)),
                new("id", Encoding.UTF8.GetBytes(message.Id)),
                new("source", Encoding.UTF8.GetBytes(message.Source)),
                new("type", Encoding.UTF8.GetBytes(message.Type)),
                new("time", Encoding.UTF8.GetBytes(message.Time.ToString())),
                new("data-content-type", Encoding.UTF8.GetBytes(contentType)),
                new("trace-id", Encoding.UTF8.GetBytes(traceId)),
            ]
        };

        IProducer<string, string> producer = serviceProvider.GetRequiredKeyedService<IProducer<string, string>>(configuration.ProducerType.ToString());

        DeliveryResult<string, string> response = await producer.ProduceAsync(configuration.Topic, kafkaMessage, cancellationToken);

        return (CoelsaPersistenceStatus)response.Status;

    }

    public async Task<CoelsaPersistenceStatus> PublishOutboxAsync<T>(MessageConfiguration configuration, CoelsaMessage<T> message, IDbConnection connection, IDbTransaction? transaction = null, CancellationToken cancellationToken = default) where T : class
    {
        string contentType = "application/json";

        string serializedMessage = "";

        if (message.DataContentType == CoelsaDataContentType.Json)
            serializedMessage = CoelsaKafkaTools.JsonSerialize(message.Data);

        string traceId = Activity.Current?.TraceId.ToString() ?? string.Empty;

        Message<string, string> kafkaMessage = new()
        {
            Key = message.Id,
            Value = serializedMessage,
            Headers = [
                new("spec-version", Encoding.UTF8.GetBytes(message.SpecVersion)),
                new("id", Encoding.UTF8.GetBytes(message.Id)),
                new("source", Encoding.UTF8.GetBytes(message.Source)),
                new("type", Encoding.UTF8.GetBytes(message.Type)),
                new("time", Encoding.UTF8.GetBytes(message.Time.ToString())),
                new("data-content-type", Encoding.UTF8.GetBytes(contentType)),
                new("trace-id", Encoding.UTF8.GetBytes(traceId)),
            ]
        };

        IProducer<string, string> producer = serviceProvider.GetRequiredKeyedService<IProducer<string, string>>(configuration.ProducerType.ToString());

        IOutboxSqlServerCommandRepository outboxRepository = serviceProvider.GetRequiredService<IOutboxSqlServerCommandRepository>();

        try
        {
            DeliveryResult<string, string> response = await producer.ProduceAsync(configuration.Topic, kafkaMessage, cancellationToken);

            bool persisted = response.Status == PersistenceStatus.Persisted;

            if (configuration.ProducerType == ProducerType.event_producer && options.Value.EventsProducer.Acks != Acks.All)
                persisted = response.Status != PersistenceStatus.NotPersisted;

            if (configuration.ProducerType == ProducerType.queue_producer && options.Value.QueuesProducer.Acks != Acks.All)
                persisted = response.Status != PersistenceStatus.NotPersisted;

            if (!persisted)
            {
                bool outboxResponse = await outboxRepository.SaveMessageAsync(new OutboxMessages
                {
                    Id = message.Id,
                    Topic = configuration.Topic,
                    Payload = serializedMessage,
                    SpecVersion = message.SpecVersion,
                    Source = message.Source,
                    Type = message.Type,
                    Time = message.Time,
                    DataContentType = contentType,
                    TraceId = traceId,
                }, connection, transaction);

                return outboxResponse ? CoelsaPersistenceStatus.Persisted : CoelsaPersistenceStatus.NotPersisted;
            }

            return (CoelsaPersistenceStatus)response.Status;

        }
        catch
        {
            bool outboxResponse = await outboxRepository.SaveMessageAsync(new OutboxMessages
            {
                Id = message.Id,
                Topic = configuration.Topic,
                Payload = serializedMessage,
                SpecVersion = message.SpecVersion,
                Source = message.Source,
                Type = message.Type,
                Time = message.Time,
                DataContentType = contentType,
                TraceId = traceId,
            }, connection, transaction);

            return outboxResponse ? CoelsaPersistenceStatus.Persisted : CoelsaPersistenceStatus.NotPersisted;
        }
    }
}
