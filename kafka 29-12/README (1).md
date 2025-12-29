# Coelsa.Artifact.Kafka

NuGet para estandarizar el uso de Apache Kafka en servicios .NET y agregar una implementacion de Outbox en SQL Server.

Incluye:

- Registro simple de consumidores Kafka (Queue y Event).
- Procesamiento tipado (deserializacion JSON a tu modelo).
- Patron Outbox en SQL Server:
  - Guardar mensajes en la tabla Outbox dentro de una transaccion de negocio.
  - Un procesador (BackgroundService) que reclama (claim) mensajes pendientes y los publica en Kafka.
  - Locks distribuidos y politicas basicas de reintentos/limpieza.

---

## 1) Instalacion

En el proyecto donde lo vas a usar:

```bash
dotnet add package Coelsa.Artifact.Kafka
```

---

## 2) Conceptos rapidos

### Queue vs Event

El paquete separa configuracion por tipo:

- **Queue**: `ProducerType.queue_producer` / `ConsumerType.queue_consumer`
- **Event**: `ProducerType.event_producer` / `ConsumerType.event_consumer`

Esto impacta en que seccion de configuracion se usa (`KafkaOptions:Queues*` o `KafkaOptions:Events*`) y el `client.id` / `group.id` del consumer.

### Mensaje tipado

Se envia/guarda un sobre con metadata:

```csharp
var msg = new CoelsaMessage<Invoice>(
    Data: invoice,
    Source: "invoice.created",
    Type: "urn:coelsa.com.ar/billing/invoice"
);
```

`CoelsaMessage<T>` contiene:

- `Data` (tu payload)
- `Source`, `Type`, `SpecVersion` (default "1.0")
- `DataContentType` (actualmente JSON)
- `Key`, `Time`, `TraceId` (autogenerados por defecto)

---

## 3) Configuracion (appsettings / secrets)

### 3.1 KafkaOptions

El paquete lee configuracion desde la seccion `KafkaOptions` (keys case-insensitive).

Estructura recomendada (ejemplo):

```json
{
  "KafkaOptions": {
    "QueuesOptions": {
      "BootstrapServers": "SecretsSettings:KafkaQueuesBootstrapServers",
      "Security": {
        "Username": "SecretsSettings:kafkaUsername",
        "Password": "SecretsSettings:kafkaPassword"
      }
    },
    "QueuesProducer": {
      "Acks": "All",
      "EnableIdempotence": true,
      "EnableTransaction": false,
      "MessageSendMaxRetries": 3,
      "RetryBackoffMs": "00:00:00.100"
    },
    "QueuesConsumer": {
      "GroupId": "mi-servicio-queue",
      "AutoOffsetReset": "Earliest",
      "EnableAutoCommit": false,
      "MaxConcurrency": 1
    },

    "EventsOptions": {
      "BootstrapServers": "SecretsSettings:KafkaEventsBootstrapServers",
      "Security": {
        "Username": "SecretsSettings:kafkaUsername",
        "Password": "SecretsSettings:kafkaPassword"
      }
    },
    "EventsProducer": {
      "Acks": "All",
      "EnableIdempotence": true,
      "EnableTransaction": false,
      "MessageSendMaxRetries": 3,
      "RetryBackoffMs": "00:00:00.100"
    },
    "EventsConsumer": {
      "GroupId": "mi-servicio-event",
      "AutoOffsetReset": "Earliest",
      "EnableAutoCommit": false,
      "MaxConcurrency": 1
    }
  }
}
```

Notas importantes:

- `QueuesOptions.BootstrapServers` y `EventsOptions.BootstrapServers` **son keys** (paths) a donde esta el valor real.
  - Por defecto el paquete usa:
    - `SecretsSettings:KafkaQueuesBootstrapServers`
    - `SecretsSettings:KafkaEventsBootstrapServers`
- `Security.Username` y `Security.Password` tambien son keys (por defecto `SecretsSettings:kafkaUsername` / `SecretsSettings:kafkaPassword`).
- `GroupId` es el group id real del consumer.
  - Si no se configura, el paquete usa el nombre del assembly como fallback.

### 3.2 OutboxOptions

El Outbox se configura con la seccion `OutboxOptions` (fuera de KafkaOptions).

Minimo recomendado:

```json
{
  "OutboxOptions": {
    "SqlServer": {
      "Initials": "ARQ",
      "ConnectionString": "OutboxConnectionString"
    }
  }
}
```

- `SqlServer.Initials` se usa como prefijo de tablas (por defecto "ARQ").
- `SqlServer.ConnectionString` es la key donde esta la connection string real.  
  Ejemplo: definir `OutboxConnectionString` en user-secrets / KeyVault / env vars.

Opcionalmente podes ajustar:

- `MaxConcurrency`
- `ProcessingInterval`
- `BatchSize`
- `Retries` (reintentos)
- limpieza de mensajes huerfanos y retencion de procesados

---

## 4) Paso a paso: Productor con Outbox (guardar en SQL Server)

Este modo NO publica directo en Kafka desde tu transaccion de negocio.  
Primero guarda en Outbox, y luego un procesador se encarga de publicar.

### 4.1 Registrar servicios (API / servicio que escribe Outbox)

En `Program.cs`:

```csharp
using Coelsa.Artifact.Kafka.Outbox;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCoelsaOutboxProducer(builder.Configuration);

var app = builder.Build();
app.Run();
```

### 4.2 Crear el esquema en SQL Server

En el repo del paquete hay un script:

- `scripts/initial_create.sql`

Ese script crea (entre otras cosas):

- `{Initials}_DISTRIBUTED_LOCKS`
- `{Initials}_OUTBOX_MESSAGES`

Reemplaza `{Initials}` por el valor configurado en `OutboxOptions:SqlServer:Initials` (ej: ARQ).

### 4.3 Guardar un mensaje en Outbox dentro de una transaccion

Inyecta `IOutboxService` y guarda el mensaje:

```csharp
using Coelsa.Artifact.Kafka.Handler.Interfaces;
using Coelsa.Artifact.Kafka.Model;
using Coelsa.Artifact.Kafka.Model.Enum;
using Coelsa.Artifact.Kafka.Support.Settings;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

app.MapPost("/outbox/invoices/{amount:int}", async (
    int amount,
    IOutboxService outbox,
    IOptions<OutboxOptions> options,
    IConfiguration configuration,
    CancellationToken ct) =>
{
    using SqlConnection connection = new(configuration[options.Value.SqlServer.ConnectionString]);
    await connection.OpenAsync(ct);

    using var tx = await connection.BeginTransactionAsync(ct);

    try
    {
        for (int i = 0; i < amount; i++)
        {
            var invoice = new Invoice { Id = Guid.NewGuid().ToString(), TotalAmount = 100 };

            var evt = new CoelsaMessage<Invoice>(
                Data: invoice,
                Source: "invoice.created",
                Type: "urn:coelsa.com.ar/billing/invoice"
            );

            bool ok = await outbox.SaveOutboxAsync(
                message: evt,
                topic: "dev_arq_stream_updated",
                producerType: ProducerType.queue_producer,
                connection: connection,
                transaction: tx,
                cancellationToken: ct
            );

            if (!ok)
                throw new Exception("No se pudo persistir el mensaje en Outbox");
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

## 5) Paso a paso: Procesar Outbox y publicar en Kafka

Esto lo podes correr:

- En el mismo servicio que escribe la Outbox (simple), o
- En un worker dedicado (recomendado para separar responsabilidades).

### 5.1 Registrar servicios del procesador

En `Program.cs` (Worker o API):

```csharp
using Coelsa.Artifact.Kafka.Outbox;
using Coelsa.Artifact.Kafka.Outbox.BackgroundServices;

var builder = WebApplication.CreateBuilder(args);

string environment = builder.Configuration.GetValue<string>("Environment") ?? "dev";

// Servicios necesarios para procesar outbox y publicar a Kafka
builder.Services.AddCoelsaOutboxService(builder.Configuration, environment);

// BackgroundService que ejecuta el loop de procesamiento
builder.Services.AddHostedService<OutboxProcessor>();

var app = builder.Build();
app.Run();
```

Que hace el procesador:

- Reclama (claim) mensajes pendientes en `{Initials}_OUTBOX_MESSAGES`.
- Publica a Kafka usando la configuracion Queue o Event segun `OMSG_PRODUCER_TYPE`.
- Marca como procesado o registra el error y aplica reintentos.

---

## 6) Paso a paso: Consumir mensajes desde Kafka

### 6.1 Registrar consumidor (Queue o Event)

En `Program.cs`:

```csharp
using Coelsa.Artifact.Kafka;

var builder = WebApplication.CreateBuilder(args);

string environment = builder.Configuration.GetValue<string>("Environment") ?? "dev";

// Para topics tipo Queue
builder.Services.AddCoelsaConsumerKafkaQueue(builder.Configuration, environment);

// Para topics tipo Event (usar uno u otro segun tu caso)
// builder.Services.AddCoelsaConsumerKafkaEvent(builder.Configuration, environment);

builder.Services.AddHostedService<ConsumerService>();

var app = builder.Build();
app.Run();
```

### 6.2 Implementar el HostedService

Ejemplo minimal:

```csharp
using Coelsa.Artifact.Kafka.Handler.Interfaces;
using Coelsa.Artifact.Kafka.Model.Enum;
using Confluent.Kafka;

public class ConsumerService(ICoelsaKafkaConsumer consumerProvider) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        IConsumer<string, string> consumer =
            consumerProvider.Subscribe("dev_arq_stream_updated", ConsumerType.queue_consumer);

        _ = Task.Run(async () =>
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await consumerProvider.ConsumeAsync<Invoice>(
                    consumer,
                    async (invoice, ct) =>
                    {
                        // tu logica
                        Console.WriteLine($"Invoice Id: {invoice?.Id}");
                        await Task.CompletedTask;
                    },
                    cancellationToken
                );
            }
        }, cancellationToken);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
```

Notas:

- `Subscribe` devuelve un `IConsumer<string,string>` ya configurado.
- `ConsumeAsync<T>` deserializa JSON a `T` y ejecuta tu handler.
- Si el handler termina bien, hace `Commit` del offset.

---

## 7) Checklist rapido para implementar en una solucion

1. Instalar el paquete `Coelsa.Artifact.Kafka`.
2. Agregar `KafkaOptions` (Queues/Events) y definir secretos:
   - `SecretsSettings:KafkaQueuesBootstrapServers` / `SecretsSettings:KafkaEventsBootstrapServers`
   - `SecretsSettings:kafkaUsername` / `SecretsSettings:kafkaPassword`
3. Si vas a usar Outbox:
   - Agregar `OutboxOptions` (Initials + ConnectionString key).
   - Crear tablas con `scripts/initial_create.sql`.
   - En el productor: `AddCoelsaOutboxProducer` + `IOutboxService.SaveOutboxAsync(...)`.
   - En el despachador: `AddCoelsaOutboxService` + `AddHostedService<OutboxProcessor>()`.
4. Si vas a consumir:
   - `AddCoelsaConsumerKafkaQueue` o `AddCoelsaConsumerKafkaEvent`.
   - Implementar un `IHostedService` que llame `Subscribe` + `ConsumeAsync<T>`.

---

## 8) Problemas comunes

- **No publica nada a Kafka**: revisar que el servicio que corre `OutboxProcessor` tenga acceso a la misma BD, y que existan mensajes en estado Pending/Processing.
- **BootstrapServers vacio**: recordar que `QueuesOptions.BootstrapServers` es una key a secrets; definir el valor real en esa key.
- **Reprocesos**: Outbox es "at least once". Asegurar idempotencia en consumidores.

---

## 9) Referencias del repositorio

- Ejemplos de uso: carpeta `example/`
- Script SQL: `scripts/initial_create.sql`
