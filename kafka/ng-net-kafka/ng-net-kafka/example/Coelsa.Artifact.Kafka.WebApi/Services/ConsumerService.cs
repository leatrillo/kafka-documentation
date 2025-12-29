using Coelsa.Artifact.Kafka.Handler.Interfaces;
using Confluent.Kafka;

namespace Coelsa.Artifact.Kafka.WebApi.Services;

public class ConsumerService(ICoelsaKafkaConsumer consumerProvider) : IHostedService
{
    public static ConsumerService? Instancia { get; private set; }

    public IConsumer<string, string>? Consumer;
    public Task StartAsync(CancellationToken cancellationToken)
    {
        Instancia = this;

        Consumer = consumerProvider.Subscribe("dev_arq_stream_updated");

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

}
