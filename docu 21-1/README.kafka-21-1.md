# Coelsa.Artifact.Kafka

Paquete NuGet para estandarizar el uso de **Apache Kafka** en aplicaciones **.NET 10** usando **Confluent.Kafka**, con soporte para:

- Productores (Queue y Event)
- Consumidores (Queue y Event)
- Patron **Outbox** en **SQL Server** con procesamiento en background

---

## Contenidos

- [Que resuelve este paquete](#que-resuelve-este-paquete)
- [Requisitos](#requisitos)
- [Instalacion](#instalacion)
- [Configuracion](#configuracion)
  - [KafkaOptions](#kafkaoptions)
  - [OutboxOptions](#outboxoptions)
- [Registro en Program.cs](#registro-en-programcs)
  - [Productor](#productor)
  - [Consumidor](#consumidor)
  - [Outbox](#outbox)
- [Uso](#uso)
  - [Enviar mensaje directo a Kafka](#enviar-mensaje-directo-a-kafka)
  - [Guardar mensaje en Outbox (SQL Server)](#guardar-mensaje-en-outbox-sql-server)
  - [Procesar Outbox y publicar en Kafka](#procesar-outbox-y-publicar-en-kafka)
  - [Consumir mensajes](#consumir-mensajes)
- [Notas de diseno y buenas practicas](#notas-de-diseno-y-buenas-practicas)
- [Troubleshooting](#troubleshooting)
- [Versionado](#versionado)

---

## Que resuelve este paquete

### Produccion
Permite publicar mensajes a Kafka de forma tipada, usando un sobre `CoelsaMessage<T>` (estilo CloudEvents simplificado) y separando configuracion por tipo de producer:

- **Queue**: `ProducerType.queue_producer`
- **Event**: `ProducerType.event_producer`

### Consumo
Expone una interfaz de consumo que:
- Suscribe a uno o varios topics (`Subscribe`)
- Consume y deserializa JSON al tipo esperado (`ConsumeAsync<T>` / `Consume<T>`)
- Maneja commit cuando el handler devuelve `true`

### Outbox (SQL Server)
Implementa el patron Outbox para garantizar "at least once" en escenarios donde:
- Queres guardar cambios de negocio en SQL Server
- Queres guardar el mensaje a publicar en la misma transaccion
- Un proceso en background toma los mensajes pendientes y los publica en Kafka

---

## Requisitos

- .NET 10 (net10.0)
- Acceso a un cluster Kafka
- SQL Server (solo si usas Outbox)
- Variables/secretos para bootstrap servers y credenciales (si aplica)

---

## Instalacion

En tu proyecto:

```bash
dotnet add package Coelsa.Artifact.Kafka
```

O en `.csproj`:

```xml
<PackageReference Include="Coelsa.Artifact.Kafka" Version="10.0.0-RC" />
```

---

## Configuracion

> Importante: el binder de .NET es case-insensitive, por eso vas a ver `kafkaOptions` en los ejemplos del repo. En la doc usamos `KafkaOptions` por claridad, pero ambas funcionan.

### KafkaOptions

Ejemplo recomendado (Queue + Event):

```json
{
  "KafkaOptions": {
    "QueuesOptions": {
      "BootstrapServers": "KafkaQueuesBootstrapServers",
      "Security": {
        "Username": "KafkaUsername",
        "Password": "KafkaPassword"
      }
    },
    "QueuesProducer": {
      "Acks": "All",
      "EnableIdempotence": true,
      "EnableTransaction": false,
      "MaxInFlight": 5,
      "Retries": 3,
      "RetryBackoffMs": "00:00:00.100"
    },
    "QueuesConsumer": {
      "GroupId": "mi-servicio-queue",
      "AutoOffsetReset": "Earliest",
      "EnableAutoCommit": false,
      "MaxConcurrency": 5
    },

    "EventsOptions": {
      "BootstrapServers": "KafkaEventsBootstrapServers",
      "Security": {
        "Username": "KafkaUsername",
        "Password": "KafkaPassword"
      }
    },
    "EventsProducer": {
      "Acks": "All",
      "EnableIdempotence": true,
      "EnableTransaction": false,
      "MaxInFlight": 5,
      "Retries": 3,
      "RetryBackoffMs": "00:00:00.100"
    },
    "EventsConsumer": {
      "GroupId": "mi-servicio-event",
      "AutoOffsetReset": "Earliest",
      "EnableAutoCommit": false,
      "MaxConcurrency": 5
    }
  }
}
```

#### Sobre BootstrapServers / Username / Password (valores indirectos)
En los ejemplos del repo, `BootstrapServers`, `Username` y `Password` se definen como **claves** (por ejemplo `KafkaQueuesBootstrapServers`).  
La libreria resuelve el valor real leyendo esa clave en `IConfiguration`.

Ejemplo de como definirlos en `appsettings.Development.json` o User-Secrets:

```json
{
  "KafkaQueuesBootstrapServers": "localhost:9092",
  "KafkaEventsBootstrapServers": "localhost:9092",
  "KafkaUsername": "",
  "KafkaPassword": ""
}
```

---

### OutboxOptions

Minimo recomendado:

```json
{
  "OutboxOptions": {
    "SqlServer": {
      "Initials": "APP",
      "ConnectionString": "OutboxConnectionString"
    }
  }
}
```

Y la connection string real se define en otra clave:

```json
{
  "OutboxConnectionString": "Server=localhost;Database=MyDb;Trusted_Connection=True;TrustServerCertificate=True"
}
```

Opcionales utiles (ejemplo):

```json
{
  "OutboxOptions": {
    "BatchSize": 100,
    "ProcessingInterval": "00:00:10",
    "MaxConcurrency": 5,

    "OrphanCleanupEnabled": true,
    "OrphanageTimeout": "00:05:00",
    "OrphanCleanupProbability": 0.1,

    "SqlServer": {
      "Initials": "APP",
      "ConnectionString": "OutboxConnectionString",
      "BatchSizeDeleteProcessed": 1000,
      "CommandTimeoutSeconds": 300
    }
  }
}
```

---

## Registro en Program.cs

### Productor

Para publicar directo a Kafka (Queue):

```csharp
using Coelsa.Artifact.Kafka;

var builder = WebApplication.CreateBuilder(args);

var environment = builder.Configuration.GetValue<string>("Environment") ?? "dev";

builder.Services.AddCoelsaKafkaQueueProducer(builder.Configuration, environment);

var app = builder.Build();
app.Run();
```

Para Event:

```csharp
builder.Services.AddCoelsaKafkaEventProducer(builder.Configuration, environment);
```

### Consumidor

Queue:

```csharp
using Coelsa.Artifact.Kafka;

var builder = WebApplication.CreateBuilder(args);

var environment = builder.Configuration.GetValue<string>("Environment") ?? "dev";

builder.Services.AddCoelsaKafkaQueueConsumer(builder.Configuration, environment);

// tu BackgroundService
builder.Services.AddHostedService<ConsumerService>();

var app = builder.Build();
app.Run();
```

Event:

```csharp
builder.Services.AddCoelsaKafkaEventConsumer(builder.Configuration, environment);
```

### Outbox

Productor (solo guarda en Outbox):

```csharp
using Coelsa.Artifact.Kafka.Outbox;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCoelsaOutboxProducer(builder.Configuration);

var app = builder.Build();
app.Run();
```

Procesador (publica en background + servicios necesarios):

```csharp
using Coelsa.Artifact.Kafka.Outbox;
using Coelsa.Artifact.Kafka.Outbox.BackgroundServices;

var builder = WebApplication.CreateBuilder(args);

var environment = builder.Configuration.GetValue<string>("Environment") ?? "dev";

builder.Services.AddCoelsaOutboxService(builder.Configuration, environment);
builder.Services.AddHostedService<OutboxProcessor>();

var app = builder.Build();
app.Run();
```

---

## Uso

### Enviar mensaje directo a Kafka

Inyecta el producer adecuado:

- `IKafkaQueueProducer` para Queue
- `IKafkaEventProducer` para Event

Ejemplo (Queue):

```csharp
using Coelsa.Artifact.Kafka.Handler.Interfaces;
using Coelsa.Artifact.Kafka.Model;

app.MapPost("/kafka/queue/orders", async (IKafkaQueueProducer producer) =>
{
    var order = new Order { Id = Guid.NewGuid().ToString(), Total = 123.45m };

    var message = new CoelsaMessage<Order>(
        Data: order,
        Source: "order.created",
        Type: "urn:coelsa.com.ar/orders/order"
    );

    await producer.PublishAsync("orders-topic", message);

    return Results.Ok(new { message.Key, message.TraceId });
});
```

> Nota: si `EnableTransaction=true`, el producer inicializa transacciones al crearse. Usar transacciones en Kafka solo cuando haga sentido en tu escenario.

---

### Guardar mensaje en Outbox (SQL Server)

Este flujo se usa cuando queres guardar dominio + mensaje en una misma transaccion.

```csharp
using Coelsa.Artifact.Kafka.Handler.Interfaces;
using Coelsa.Artifact.Kafka.Model;
using Coelsa.Artifact.Kafka.Model.Enum;
using Coelsa.Artifact.Kafka.Support.Settings;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

app.MapPost("/outbox/orders/{amount:int}", async (
    int amount,
    IOutboxService outbox,
    IOptions<OutboxOptions> options,
    IConfiguration configuration,
    CancellationToken ct) =>
{
    var csKey = options.Value.SqlServer.ConnectionString;
    var connectionString = configuration[csKey];

    using var connection = new SqlConnection(connectionString);
    await connection.OpenAsync(ct);

    using var tx = await connection.BeginTransactionAsync(ct);

    try
    {
        for (int i = 0; i < amount; i++)
        {
            var order = new Order { Id = Guid.NewGuid().ToString(), Total = 100 };

            var message = new CoelsaMessage<Order>(
                Data: order,
                Source: "order.created",
                Type: "urn:coelsa.com.ar/orders/order"
            );

            var ok = await outbox.SaveOutboxAsync(
                message: message,
                topic: "orders-topic",
                producerType: ProducerType.queue_producer,
                connection: connection,
                transaction: tx,
                cancellationToken: ct
            );

            if (!ok)
                throw new Exception("No se pudo guardar el mensaje en Outbox");
        }

        await tx.CommitAsync(ct);
        return Results.Ok(new { Status = "ok", Count = amount });
    }
    catch
    {
        await tx.RollbackAsync(ct);
        throw;
    }
});
```

---

### Procesar Outbox y publicar en Kafka

Corre el `OutboxProcessor` como `HostedService` (ver seccion [Outbox](#outbox)).  
Internamente el procesador hace fases tipo:

- Claim (reclamar mensajes pendientes con lock distribuido)
- Procesar (publicar a Kafka segun producer type)
- Marcar procesado / error y reintentos
- Limpieza de huerfanos (segun configuracion)

---

### Consumir mensajes

El consumo se hace con `ICoelsaKafkaConsumer`.

Ejemplo de `BackgroundService`:

```csharp
using Coelsa.Artifact.Kafka.Handler.Interfaces;
using Coelsa.Artifact.Kafka.Model.Enum;
using Confluent.Kafka;

public sealed class OrderConsumerService(ICoelsaKafkaConsumer consumerProvider) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        IConsumer<string, string> consumer =
            consumerProvider.Subscribe("orders-topic", ConsumerType.queue_consumer);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var processed = await consumerProvider.ConsumeAsync<Order>(
                    consumer,
                    async (order, ct) =>
                    {
                        // tu logica
                        Console.WriteLine($"Order Id: {order?.Id}");
                        await Task.CompletedTask;

                        return true; // commit del offset
                    },
                    stoppingToken
                );

                if (!processed)
                    await Task.Delay(1000, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // shutdown
            }
            catch
            {
                await Task.Delay(1000, stoppingToken);
            }
        }
    }
}
```

---

## Notas de diseno y buenas practicas

- Outbox es **at least once**: los consumidores deben ser **idempotentes**.
- Evitar hardcodear secretos: usar user-secrets, KeyVault, variables de entorno o el mecanismo que use el proyecto.
- Si usas transacciones de Kafka, validar tu caso de uso: pueden agregar complejidad operativa.
- Definir convenciones de topics (nombres, particionamiento, retencion) a nivel arquitectura.

---

## Troubleshooting

**1) BootstrapServers queda vacio**
- Revisar si `KafkaOptions:QueuesOptions:BootstrapServers` tiene una clave indirecta.
- Confirmar que la clave exista en la configuracion final (appsettings.Development, user-secrets, env vars).

**2) El consumer no procesa**
- Verificar `GroupId`, topic, y `AutoOffsetReset`.
- Revisar logs y confirmar que haya mensajes en el topic.
- Confirmar `MaxConcurrency` y `EnableAutoCommit=false` (si el handler devuelve `true`, se hace commit).

**3) Outbox acumula mensajes**
- Confirmar que el servicio con `OutboxProcessor` esta corriendo.
- Verificar connection string y permisos en SQL Server.
- Revisar configuracion de `ProcessingInterval`, `BatchSize`, `MaxConcurrency`.

---

## Versionado

- PackageId: `Coelsa.Artifact.Kafka`
- TargetFramework: `net10.0`
- Version actual (csproj): `10.0.0-RC`
