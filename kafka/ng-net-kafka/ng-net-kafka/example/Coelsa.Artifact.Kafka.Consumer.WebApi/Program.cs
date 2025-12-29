using Coelsa.Artifact.Kafka;
using Coelsa.Artifact.Kafka.Consumer.WebApi.Models;
using Coelsa.Artifact.Kafka.Consumer.WebApi.Services;
using Coelsa.Artifact.Kafka.Handler.Interfaces;
using Confluent.Kafka;
using Microsoft.AspNetCore.Mvc;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

string? environment = builder.Configuration.GetValue<string>("Environment");

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddHostedService<ConsumerService>();

builder.Services.AddCoelsaProducerKafka(builder.Configuration, environment ?? "dev");

builder.Services.AddCoelsaConsumerKafka(builder.Configuration, environment ?? "dev");

builder.Services.AddSwaggerGen();

WebApplication app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapGet("/kafka-consumer/{amount}", async ([FromRoute] int amount, [FromServices] ICoelsaKafkaConsumer consumerProvider) =>
{
    IConsumer<string, string> consumer = ConsumerService.Instancia.Consumer;

    try
    {
        for (int i = 0; i < amount; i++)
        {
            _ = await consumerProvider.ConsumeAsync<Invoice>(consumer, async (invoice, ct) =>
            {
                // Procesar el mensaje recibido
                await Task.Delay(100, ct); // Simular procesamiento

                Console.WriteLine($"Received message: " +
               $"Invoice Id: {invoice?.Id}, Customer: {invoice?.CustomerId}, " +
               $"Cantidad: {invoice?.TotalAmount}");

                return true; // Indicar que el mensaje fue procesado correctamente
            }, default);
        }

    }
    catch (Exception ex)
    {
        Console.WriteLine($"{ex}");
        throw;
    }


})
.WithOpenApi();

app.Run();
