# Coelsa.Artifact.Kafka

Integracion con Apache Kafka para servicios .NET usando un modelo simple de productor y consumidor, mas soporte para el patron Outbox en SQL Server.

Este paquete apunta a resolver:

- Publicar eventos de dominio en Kafka de forma confiable.
- Consumir mensajes de manera tipada sin tener que trabajar directo con strings y configuraciones de Confluent.
- Opcionalmente, persistir mensajes en una tabla Outbox de SQL Server usando `PublishOutboxAsync`.

La libreria expone principalmente:

- Metodos de extension para registrar productores y consumidores:
  - `AddCoelsaProducerKafka`
  - `AddCoelsaConsumerKafka`
- La interfaz `ICoelsaKafkaProducer` para publicar mensajes:
  - `PublishAsync`
  - `PublishOutboxAsync`
- La interfaz `ICoelsaKafkaConsumer` para suscribirse y consumir mensajes:
  - `Subscribe`
  - `Consume` / `ConsumeAsync`
- Tipos de soporte: `CoelsaMessage<T>`, `MessageConfiguration`, `CoelsaPersistenceStatus`, `ProducerType`, `ConsumerType`, entre otros.

> Nota: este README describe el uso basico del paquete. Ajusta nombres de namespaces y ejemplos a tu solucion si fuese necesario.

---

## 1. Paquetes

### 1.1. Productor y consumidor Kafka

Paquete principal:

```bash
dotnet add package Coelsa.Artifact.Kafka
```

### 1.2. Outbox en SQL Server (opcional)

Si queres persistir mensajes en una tabla Outbox en SQL Server, agrega ademas:

```bash
dotnet add package Coelsa.Artifact.Outbox.SqlServer
```

---

## 2. Configuracion

La configuracion se hace a traves de la seccion `KafkaOptions` del `appsettings.json`.  
El tipo `KafkaOptions` se mapea automaticamente usando `CoelsaKafkaTools.AddSettings`.

Ejemplo de configuracion minima:

```json
{
  "KafkaOptions": {
    "QueuesProducer": {
      "BootstrapServers": "localhost:9092"
    },
    "QueuesConsumer": {
      "BootstrapServers": "localhost:9092",
      "GroupId": "mi-servicio-queue",
      "AutoOffsetReset": "Earliest"
    },
    "EventsProducer": {
      "BootstrapServers": "localhost:9092"
    },
    "EventsConsumer": {
      "BootstrapServers": "localhost:9092",
      "GroupId": "mi-servicio-event",
      "AutoOffsetReset": "Earliest"
    },
    "Security": {
      "Username": "",
      "Password": ""
    },
    "SchemaRegistry": {
      "UseAvro": false,
      "Url": "http://localhost:8081",
      "RequestTimeout": "00:00:30"
    },
    "SqlServer": {
      "Initials": "ARQ",
      "Retries": 3,
      "RetryIntervalSeconds": 2
    },
    "Outbox": {
      "MaxConcurrency": 1
    }
  }
}
```

Notas rapidas:

- `QueuesProducer` / `QueuesConsumer`: configuracion por defecto para producers y consumers de tipo queue.
- `EventsProducer` / `EventsConsumer`: configuracion para producers y consumers de tipo event.
- `Security`: usuario y password SASL en caso de usar autenticacion.
- `SchemaRegistry`: opciones para Schema Registry si se usa Avro (`UseAvro = true`), si no se usa se mantiene en `false` y se serializa en JSON.
- `SqlServer` y `Outbox`: usadas por el soporte de Outbox en SQL Server.

---

## 3. Registro en DI

Los metodos de extension viven en `Coelsa.Artifact.Kafka.KafkaExtensions` y en `Coelsa.Artifact.Outbox.SqlServer.OutboxSqlServerExtensions`.

### 3.1. API productora de eventos

Ejemplo en `Program.cs` de una Web API:

```csharp
using Coelsa.Artifact.Kafka;
using Coelsa.Artifact.Kafka.Handler.Interfaces;
using Coelsa.Artifact.Outbox.SqlServer;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

string? environment = builder.Configuration.GetValue<string>("Environment");

// Registro de servicios propios
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Registro de Kafka (productor) y Outbox SqlServer (opcional)
builder.Services.AddCoelsaProducerKafka(builder.Configuration, environment ?? "dev");
builder.Services.AddCoelsaOutboxSqlServer();

WebApplication app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();
```

### 3.2. Worker consumidor de eventos

Ejemplo en `Program.cs` de un Worker:

```csharp
using Coelsa.Artifact.Kafka;
using Coelsa.Artifact.Kafka.Handler.Interfaces;
using Coelsa.Artifact.Kafka.Consumer.Services;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        string? environment = context.Configuration.GetValue<string>("Environment");

        services.AddHostedService<ConsumerService>();

        // Registrar productor y consumidor de Kafka
        services.AddCoelsaProducerKafka(context.Configuration, environment ?? "dev");
        services.AddCoelsaConsumerKafka(context.Configuration, environment ?? "dev");
    })
    .Build();

await host.RunAsync();
```

El `ConsumerService` es un `IHostedService` donde se usa `ICoelsaKafkaConsumer` para suscribirse y procesar mensajes.

---

## 4. Publicar mensajes con ICoelsaKafkaProducer

La interfaz principal para enviar mensajes es `ICoelsaKafkaProducer`.

Firmas relevantes:

```csharp
public interface ICoelsaKafkaProducer
{
    Task<CoelsaPersistenceStatus> PublishAsync<T>(
        MessageConfiguration configuration,
        CoelsaMessage<T> message,
        CancellationToken cancellationToken = default) where T : class;

    Task<CoelsaPersistenceStatus> PublishOutboxAsync<T>(
        MessageConfiguration configuration,
        CoelsaMessage<T> message,
        IDbConnection connection,
        CancellationToken cancellationToken = default) where T : class;
}
```

### 4.1. Enviar mensaje directo a Kafka (PublishAsync)

Ejemplo sencillo en un endpoint:

```csharp
app.MapPost("/invoices/direct", async (
    [FromServices] ICoelsaKafkaProducer producer,
    [FromBody] InvoiceRequest request) =>
{
    Invoice data = new()
    {
        Id = Guid.NewGuid().ToString(),
        CustomerId = request.CustomerId,
        TotalAmount = request.TotalAmount,
        Currency = request.Currency
    };

    CoelsaMessage<Invoice> evt = new(
        data,
        source: "invoice.created",
        type: "urn:coelsa.com.ar/billing/invoice");

    MessageConfiguration topicConfiguration = new("dev_arq_stream_updated");

    CoelsaPersistenceStatus response = await producer.PublishAsync(topicConfiguration, evt);

    return Results.Ok(new { Status = response.ToString(), MessageId = evt.Id });
});
```

En este modo el mensaje se envia directo a Kafka usando las opciones configuradas para el tipo de productor (`ProducerType.queue_producer` por defecto).

### 4.2. Guardar mensaje en Outbox de SQL Server (PublishOutboxAsync)

Para usar Outbox tenes que:

1. Registrar el paquete de SQL Server:

   ```csharp
   services.AddCoelsaOutboxSqlServer();
   ```

2. Tener una conexion `IDbConnection` valida a la base de datos donde existe la tabla Outbox.

3. Usar `PublishOutboxAsync` dentro de una operacion de negocio (idealmente dentro de una transaccion).

Ejemplo simplificado:

```csharp
app.MapPost("/invoices/outbox", async (
    [FromServices] ICoelsaKafkaProducer producer,
    [FromServices] IConfiguration configuration,
    [FromBody] InvoiceRequest request) =>
{
    Invoice data = new()
    {
        Id = Guid.NewGuid().ToString(),
        CustomerId = request.CustomerId,
        TotalAmount = request.TotalAmount,
        Currency = request.Currency
    };

    CoelsaMessage<Invoice> evt = new(
        data,
        source: "invoice.created",
        type: "urn:coelsa.com.ar/billing/invoice");

    MessageConfiguration topicConfiguration = new("dev_arq_stream_updated");

    string connectionString = configuration.GetConnectionString("SqlServerConnection") ?? string.Empty;

    using IDbConnection connection = new SqlConnection(connectionString);
    connection.Open();

    CoelsaPersistenceStatus response = await producer.PublishOutboxAsync(topicConfiguration, evt, connection);

    return Results.Ok(new { Status = response.ToString(), MessageId = evt.Id });
});
```

En este caso el mensaje se persiste en la tabla Outbox usando el esquema interno `OutboxMessages` definido por el paquete, y `CoelsaPersistenceStatus` indica si se pudo guardar correctamente.

> Importante: segun tu escenario, el procesamiento y envio posterior de Outbox a Kafka puede requerir un servicio adicional (por ejemplo un `BackgroundService` como `OutboxProcessorService`).

---

## 5. Consumir mensajes con ICoelsaKafkaConsumer

La interfaz principal del consumidor es `ICoelsaKafkaConsumer`.

Metodos clave:

```csharp
public interface ICoelsaKafkaConsumer
{
    IConsumer<string, string> Subscribe(string topic, ConsumerType consumerType = ConsumerType.queue_consumer);

    IConsumer<string, string> Subscribe(List<string> topics, ConsumerType consumerType = ConsumerType.queue_consumer);

    Task<bool> ConsumeAsync<T>(
        IConsumer<string, string> consumer,
        Func<T?, CancellationToken, Task> handler,
        CancellationToken cancellationToken = default) where T : class;

    bool Consume<T>(
        IConsumer<string, string> consumer,
        Func<T?, CancellationToken, bool> handler,
        CancellationToken cancellationToken = default) where T : class;
}
```

### 5.1. Ejemplo con IHostedService

Ejemplo basado en `ConsumerService` del proyecto de ejemplo:

```csharp
public class ConsumerService(ICoelsaKafkaConsumer consumerProvider) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        IConsumer<string, string> consumer =
            consumerProvider.Subscribe("dev_arq_stream_updated", ConsumerType.queue_consumer);

        Task.Run(async () =>
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                _ = await consumerProvider.ConsumeAsync<Invoice>(consumer, async (invoice, ct) =>
                {
                    // Procesar el mensaje recibido
                    Console.WriteLine(
                        $"Received invoice Id: {invoice?.Id}, Customer: {invoice?.CustomerId}, Amount: {invoice?.TotalAmount}");

                    await Task.CompletedTask;
                }, cancellationToken);
            }
        }, cancellationToken);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
```

Notas:

- `Subscribe` crea y configura internamente un `IConsumer<string, string>` de Confluent en base a `QueuesConsumer` o `EventsConsumer`.
- `ConsumeAsync<T>` se encarga de leer el mensaje, deserializarlo a `T` y ejecutar el `handler`.
- El `handler` recibe el objeto deserializado y un `CancellationToken`.

---

## 6. Esquema de Outbox en SQL Server

El paquete `Coelsa.Artifact.Outbox.SqlServer` usa internamente un modelo `OutboxMessages` para persistir los mensajes.

Un esquema tipico de tabla podria ser:

```sql
CREATE TABLE [dbo].[OutboxMessages] (
    [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    [Topic] NVARCHAR(200) NOT NULL,
    [Payload] NVARCHAR(MAX) NOT NULL,
    [SpecVersion] NVARCHAR(10) NOT NULL,
    [Source] NVARCHAR(200) NOT NULL,
    [Type] NVARCHAR(200) NOT NULL,
    [Time] DATETIME2 NOT NULL,
    [DataContentType] NVARCHAR(50) NOT NULL,
    [TraceId] NVARCHAR(50) NULL,
    [CreatedOnUtc] DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    [ProcessedOnUtc] DATETIME2 NULL,
    [Error] NVARCHAR(MAX) NULL
);
```

Segun tus necesidades podes extender el modelo (por ejemplo campos de tenant, correlacion, etc).  
Lo importante es respetar las columnas usadas por el paquete.

---

## 7. Desarrollo local

Para probar el paquete en desarrollo:

1. Levantar un cluster Kafka local (por ejemplo usando Docker).
2. Configurar `KafkaOptions.QueuesProducer` y `QueuesConsumer` apuntando a `localhost:9092`.
3. Configurar una cadena de conexion `SqlServerConnection` si vas a usar Outbox.
4. Levantar:
   - Una API que publique mensajes (`PublishAsync` o `PublishOutboxAsync`).
   - Un Worker que use `ICoelsaKafkaConsumer` para leer del mismo topic.

---

## 8. Resumen

`Coelsa.Artifact.Kafka` provee:

- Un wrapper de alto nivel sobre Confluent.Kafka para productores y consumidores.
- Tipos tipados (`CoelsaMessage<T>`, `MessageConfiguration`) para evitar strings magicos.
- Opciones de configuracion centralizadas en `KafkaOptions`.
- Integracion opcional con Outbox en SQL Server a traves de `PublishOutboxAsync` y el paquete `Coelsa.Artifact.Outbox.SqlServer`.

La idea es reducir el codigo repetitivo de integracion con Kafka y estandarizar como se producen y consumen mensajes dentro de tus servicios.
