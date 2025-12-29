using Coelsa.Artifact.Kafka.Handler;
using Coelsa.Artifact.Kafka.Handler.Interfaces;
using Coelsa.Artifact.Kafka.Model.Enum;
using Coelsa.Artifact.Kafka.Support;
using Coelsa.Artifact.Kafka.Support.Settings;
using System.Reflection;

namespace Coelsa.Artifact.Kafka;
public static class KafkaExtensions
{
    public static IServiceCollection AddCoelsaProducerKafka(this IServiceCollection services, IConfiguration configuration, string environment)
    {
        KafkaOptions kafkaOptions = CoelsaKafkaTools.AddSettings(services, configuration);

        ProducerOptions queueOptions = kafkaOptions.QueuesProducer;

        string? assemblyName = Assembly.GetEntryAssembly()?.GetName().Name;

        string hostname = Environment.MachineName;

        ArgumentNullException.ThrowIfNull(assemblyName);

        ArgumentNullException.ThrowIfNull(environment);

        if (!string.IsNullOrWhiteSpace(queueOptions.BootstrapServers))
        {
            string queueClientId = $"{assemblyName.ToLower()}.queue-producer.{environment}.{hostname}";

            ProducerConfig queueProducerConfig = new()
            {
                BootstrapServers = queueOptions.BootstrapServers,
                ClientId = queueClientId.ToLower(),
                Acks = queueOptions.Acks,
                EnableIdempotence = queueOptions.EnableIdempotence,
                RequestTimeoutMs = (int)queueOptions.RequestTimeout.TotalMilliseconds,
                MessageTimeoutMs = (int)queueOptions.MaxBlockMs.TotalMilliseconds,
                BatchSize = queueOptions.BatchSize,
                LingerMs = queueOptions.LingerMs.TotalMilliseconds,
                CompressionType = queueOptions.CompressionType,
                MaxInFlight = queueOptions.MaxInFlight,
                MessageSendMaxRetries = queueOptions.Retries,
                RetryBackoffMs = (int)queueOptions.RetryBackoffMs.TotalMilliseconds,
                SecurityProtocol = SecurityProtocol.SaslSsl,
                SaslMechanism = SaslMechanism.Plain,
                SaslUsername = kafkaOptions.Security.Username,
                SaslPassword = kafkaOptions.Security.Password,
                EnableSslCertificateVerification = false
            };

            services.AddKeyedSingleton(ProducerType.queue_producer.ToString(), (sp, key) => new ProducerBuilder<string, string>(queueProducerConfig).Build()
            );
        }

        ProducerOptions enventOptions = kafkaOptions.EventsProducer;

        if (!string.IsNullOrWhiteSpace(enventOptions.BootstrapServers))
        {
            string eventClientId = $"{assemblyName.ToLower()}.event-producer.{environment}.{hostname}";

            ProducerConfig eventProducerConfig = new()
            {
                BootstrapServers = enventOptions.BootstrapServers,
                ClientId = eventClientId.ToLower(),
                Acks = enventOptions.Acks,
                EnableIdempotence = enventOptions.EnableIdempotence,
                RequestTimeoutMs = (int)enventOptions.RequestTimeout.TotalMilliseconds,
                MessageTimeoutMs = (int)enventOptions.MaxBlockMs.TotalMilliseconds,
                BatchSize = enventOptions.BatchSize,
                LingerMs = enventOptions.LingerMs.TotalMilliseconds,
                CompressionType = enventOptions.CompressionType,
                MaxInFlight = enventOptions.MaxInFlight,
                MessageSendMaxRetries = enventOptions.Retries,
                RetryBackoffMs = (int)enventOptions.RetryBackoffMs.TotalMilliseconds,
                SecurityProtocol = SecurityProtocol.SaslSsl,
                SaslMechanism = SaslMechanism.Plain,
                SaslUsername = kafkaOptions.Security.Username,
                SaslPassword = kafkaOptions.Security.Password,
                EnableSslCertificateVerification = false
            };

            services.AddKeyedSingleton(ProducerType.event_producer.ToString(), (sp, key) => new ProducerBuilder<string, string>(eventProducerConfig).Build()
            );
        }

        services.AddSingleton<ICoelsaKafkaProducer, KafkaProducerHandler>();

        return services;
    }

    public static IServiceCollection AddCoelsaConsumerKafka(this IServiceCollection services, IConfiguration configuration, string environment)
    {
        KafkaOptions kafkaOptions = CoelsaKafkaTools.AddSettings(services, configuration);

        ConsumerOptions queueOptions = kafkaOptions.QueuesConsumer;

        string? assemblyName = Assembly.GetEntryAssembly()?.GetName().Name;

        string hostname = Environment.MachineName;

        ArgumentNullException.ThrowIfNull(assemblyName);

        ArgumentNullException.ThrowIfNull(environment);

        if (!string.IsNullOrWhiteSpace(queueOptions.BootstrapServers))
        {
            string queueGroupId = string.IsNullOrWhiteSpace(queueOptions.GroupId) ? assemblyName : queueOptions.GroupId;

            string queueClientId = $"{assemblyName.ToLower()}.queue-consumer.{environment}.{hostname}";

            ConsumerConfig queueConsumerConfig = new()
            {
                BootstrapServers = queueOptions.BootstrapServers,
                ClientId = queueClientId.ToLower(),
                GroupId = queueGroupId,
                AutoOffsetReset = queueOptions.AutoOffsetReset,
                EnableAutoCommit = queueOptions.EnableAutoCommit,
                SessionTimeoutMs = (int)queueOptions.SessionTimeout.TotalMilliseconds,
                HeartbeatIntervalMs = (int)queueOptions.HeartbeatInterval.TotalMilliseconds,
                MaxPollIntervalMs = (int)queueOptions.ConsumeTimeout.TotalMilliseconds,
                FetchMinBytes = queueOptions.FetchMinBytes,
                EnablePartitionEof = queueOptions.EnablePartitionEof,
                SecurityProtocol = SecurityProtocol.SaslSsl,
                SaslMechanism = SaslMechanism.Plain,
                SaslUsername = kafkaOptions.Security.Username,
                SaslPassword = kafkaOptions.Security.Password,
                EnableSslCertificateVerification = false
            };

            services.AddKeyedSingleton(ConsumerType.queue_consumer.ToString(), (sp, key) => new ConsumerBuilder<string, string>(queueConsumerConfig).Build()
            );
        }

        ConsumerOptions eventOptions = kafkaOptions.EventsConsumer;

        if (!string.IsNullOrWhiteSpace(queueOptions.BootstrapServers))
        {
            string eventGroupId = string.IsNullOrWhiteSpace(eventOptions.GroupId) ? assemblyName : eventOptions.GroupId;

            string eventClientId = $"{assemblyName.ToLower()}.event-consumer.{environment}.{hostname}";

            ConsumerConfig eventConsumerConfig = new()
            {
                BootstrapServers = eventOptions.BootstrapServers,
                ClientId = $"client.{eventClientId.ToLower()}",
                GroupId = eventGroupId,
                AutoOffsetReset = eventOptions.AutoOffsetReset,
                EnableAutoCommit = eventOptions.EnableAutoCommit,
                SessionTimeoutMs = (int)eventOptions.SessionTimeout.TotalMilliseconds,
                HeartbeatIntervalMs = (int)eventOptions.HeartbeatInterval.TotalMilliseconds,
                MaxPollIntervalMs = (int)eventOptions.ConsumeTimeout.TotalMilliseconds,
                FetchMinBytes = eventOptions.FetchMinBytes,
                EnablePartitionEof = eventOptions.EnablePartitionEof,
                SecurityProtocol = SecurityProtocol.SaslSsl,
                SaslMechanism = SaslMechanism.Plain,
                SaslUsername = kafkaOptions.Security.Username,
                SaslPassword = kafkaOptions.Security.Password,
                EnableSslCertificateVerification = false
            };

            services.AddKeyedSingleton(ConsumerType.event_consumer.ToString(), (sp, key) => new ConsumerBuilder<string, string>(eventConsumerConfig).Build()
            );
        }

        services.AddSingleton<ICoelsaKafkaConsumer, KafkaConsumerHandler>();

        return services;
    }

}
