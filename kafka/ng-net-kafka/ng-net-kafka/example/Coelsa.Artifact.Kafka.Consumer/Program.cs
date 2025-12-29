using Coelsa.Artifact.Kafka;
using Coelsa.Artifact.Kafka.Consumer.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

string? environment = builder.Configuration.GetValue<string>("Environment");

builder.Services.AddLogging();
builder.Services.AddCoelsaConsumerKafka(builder.Configuration, environment ?? "dev");
builder.Services.AddCoelsaProducerKafka(builder.Configuration, environment ?? "dev");

// Background loop consumer
builder.Services.AddHostedService<ConsumerService>();

IHost host = builder.Build();
await host.RunAsync();
