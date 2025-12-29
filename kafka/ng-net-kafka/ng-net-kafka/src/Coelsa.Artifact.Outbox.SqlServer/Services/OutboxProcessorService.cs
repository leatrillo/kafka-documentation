using Coelsa.Artifact.Kafka.Support.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Coelsa.Artifact.Outbox.SqlServer.Services;

public sealed class OutboxProcessorService(IConfiguration configuration) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        KafkaOptions options = configuration.GetSection(nameof(KafkaOptions)).Get<KafkaOptions>() ?? new();

        using SemaphoreSlim semaphore = new(options.Outbox.MaxConcurrency, options.Outbox.MaxConcurrency);

        while (!stoppingToken.IsCancellationRequested)
        {
            await semaphore.WaitAsync(stoppingToken);

            // Create a task for processing and track it to prevent memory leaks
            // This allows for concurrent processing up to MaxConcurrency
            //var processingTask = ProcessOutboxMessagesAsync(semaphore, stoppingToken);
            //_taskTracker.TrackTask(processingTask, description: "Outbox message processing");

            //// Wait for the processing interval before starting the next cycle
            //await Task.Delay(_options.Outbox.ProcessingInterval, stoppingToken);

        }

        throw new NotImplementedException();
    }
}
