using Coelsa.Artifact.Kafka.Consumer.Models;
using Coelsa.Artifact.Kafka.Handler.Interfaces;
using Coelsa.Artifact.Kafka.Model.Enum;
using Confluent.Kafka;
using Microsoft.Extensions.Hosting;

namespace Coelsa.Artifact.Kafka.Consumer.Services;

public class ConsumerService(ICoelsaKafkaConsumer consumerProvider) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        IConsumer<string, string> consumer = consumerProvider.Subscribe("dev_arq_stream_updated", ConsumerType.queue_consumer);

        Task.Run(() =>
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    _ = consumerProvider.ConsumeAsync<Invoice>(consumer, async (invoice, ct) =>
                    {
                        // Procesar el mensaje recibido
                        await Task.Delay(100, ct); // Simular procesamiento

                        Console.WriteLine($"Received message: " +
                       $"Invoice Id: {invoice?.Id}, Customer: {invoice?.CustomerId}, " +
                       $"Cantidad: {invoice?.TotalAmount}");

                        return true; // Indicar que el mensaje fue procesado correctamente
                    }, cancellationToken).GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"{ex}");
                    throw;
                }

            }
        }, cancellationToken);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

}