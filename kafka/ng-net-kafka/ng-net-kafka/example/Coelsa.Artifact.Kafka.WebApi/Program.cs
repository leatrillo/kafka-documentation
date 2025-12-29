using Coelsa.Artifact.Kafka;
using Coelsa.Artifact.Kafka.Handler.Interfaces;
using Coelsa.Artifact.Kafka.Model;
using Coelsa.Artifact.Kafka.Model.Enum;
using Coelsa.Artifact.Kafka.WebApi.Models;
using Coelsa.Artifact.Kafka.WebApi.Services;
using Coelsa.Artifact.Outbox.SqlServer;
using Confluent.Kafka;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddHostedService<ConsumerService>();

string? environment = builder.Configuration.GetValue<string>("Environment");

builder.Services.AddCoelsaProducerKafka(builder.Configuration, environment ?? "dev");

builder.Services.AddCoelsaOutboxSqlServer();

builder.Services.AddCoelsaConsumerKafka(builder.Configuration, environment ?? "dev");

builder.Services.AddSwaggerGen();

WebApplication app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapGet("/kafka-producer/{amount}/{key}", async ([FromRoute] int amount, [FromRoute] int? key, [FromServices] ICoelsaKafkaProducer producer) =>
{
    if (amount < 1)
        amount = 1;

    for (int i = 0; i < amount; i++)
    {
        Invoice data = new()
        {
            Id = Guid.NewGuid().ToString(),
            CustomerId = i + 1,
            TotalAmount = new Random().Next(100, 10000),
            Currency = i % 2 == 0 ? "USD" : "ARS"
        };

        CoelsaMessage<Invoice> evt = new(
            data, "invoice.created", "urn:coelsa.com.ar/billing/invoice")
        {
            Id = key == null ? Guid.NewGuid().ToString() : key.Value.ToString(),
        };

        MessageConfiguration topicConfiguration = new("dev_arq_stream_updated");

        try
        {
            CoelsaPersistenceStatus response = await producer.PublishAsync(topicConfiguration, evt);

            if (response.Equals(PersistenceStatus.Persisted))
                Console.WriteLine($"Persisted delivered to {evt.Id}");
            else
                Console.WriteLine($"NotPersisted delivered to {evt.Id}");
        }
        catch (Exception)
        {
            Console.WriteLine($"Debes manejar los errores en caso de excepción");
        }

    }

    return $"Se enviaron {amount} mensajes.";
})
.WithOpenApi();

app.MapGet("/kafka-outbox-producer/{amount}", async ([FromRoute] int amount, [FromServices] ICoelsaKafkaProducer producer, [FromServices] IConfiguration configuration) =>
{
    if (amount < 1)
        amount = 1;

    for (int i = 0; i < amount; i++)
    {
        Invoice data = new()
        {
            Id = Guid.NewGuid().ToString(),
            CustomerId = i + 1,
            TotalAmount = new Random().Next(100, 10000),
            Currency = i % 2 == 0 ? "USD" : "ARS"
        };

        CoelsaMessage<Invoice> evt = new(
            data, "invoice.created", "urn:coelsa.com.ar/billing/invoice")
        {
            Id = Guid.NewGuid().ToString()
        };

        MessageConfiguration topicConfiguration = new("dev_arq_stream_updated");

        string connectionString = configuration.GetConnectionString("SqlServerConnection") ?? "";

        using IDbConnection connection = new SqlConnection(connectionString);
        try
        {
            CoelsaPersistenceStatus response = await producer.PublishOutboxAsync(topicConfiguration, evt, connection);

            if (response.Equals(PersistenceStatus.Persisted))
                Console.WriteLine($"Persisted delivered to {evt.Id}");
            else
                Console.WriteLine($"NotPersisted delivered to {evt.Id}");
        }
        catch (Exception)
        {
            Console.WriteLine($"Debes manejar los errores en caso de excepción");
        }


    }

    return $"Se enviaron {amount} mensajes.";
})
.WithOpenApi();

app.Run();

