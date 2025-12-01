# Coelsa.Artifact.MessageBroker.Kafka

Integracion con Kafka para servicios .NET usando el patron Outbox/Inbox.

Este paquete provee:

- Forma confiable de publicar eventos a Kafka usando una tabla **Outbox**.
- Workers en background para despachar mensajes de Outbox hacia Kafka.
- Soporte de **Inbox** del lado consumidor (handlers idempotentes).
- APIs simples para publicar y consumir eventos de dominio.

> Nota: los nombres de tipos y metodos en este archivo son ejemplos.  
> Ajustalos para que coincidan con las clases reales del paquete (metodos de extension, clases de opciones, etc.).

---

## 1. Instalacion

```bash
dotnet add package Coelsa.Artifact.MessageBroker.Kafka
```

Agrega el paquete a cualquier servicio que necesite publicar o consumir mensajes via Kafka.

---

## 2. Configuracion

### 2.1. appsettings.json

Agrega una seccion Kafka con los datos de conexion y opciones basicas:

```json
{
  "Kafka": {
    "BootstrapServers": "localhost:9092",
    "Producer": {
      "ClientId": "my-service-api"
    },
    "Consumer": {
      "GroupId": "my-service-worker",
      "AutoOffsetReset": "Earliest"
    },
    "Outbox": {
      "Enabled": true,
      "PollingIntervalSeconds": 5
    },
    "Inbox": {
      "Enabled": true,
      "PollingIntervalSeconds": 5
    }
  }
}
```

Campos tipicos:

- `BootstrapServers`: endpoint(s) de Kafka, por ejemplo `localhost:9092` o `<cluster>:9092`.
- `Consumer.GroupId`: identificador de grupo para el consumidor (uno por servicio/worker).
- `Outbox` y `Inbox`: opciones para los workers (intervalo de polling, habilitado, etc.).

### 2.2. Tablas de base de datos (Outbox / Inbox)

El paquete asume que existe almacenamiento para **Outbox** e **Inbox** (por ejemplo SQL Server).

Ejemplo minimo de tabla Outbox:

```sql
CREATE TABLE [dbo].[OutboxMessages] (
    [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    [OccurredOnUtc] DATETIME2 NOT NULL,
    [ProcessedOnUtc] DATETIME2 NULL,
    [Type] NVARCHAR(250) NOT NULL,
    [Payload] NVARCHAR(MAX) NOT NULL,
    [Topic] NVARCHAR(200) NOT NULL,
    [Error] NVARCHAR(MAX) NULL
);
```

Ejemplo minimo de tabla Inbox (para procesamiento idempotente):

```sql
CREATE TABLE [dbo].[InboxMessages] (
    [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    [OccurredOnUtc] DATETIME2 NOT NULL,
    [ProcessedOnUtc] DATETIME2 NULL,
    [Type] NVARCHAR(250) NOT NULL,
    [Payload] NVARCHAR(MAX) NOT NULL,
    [SourceTopic] NVARCHAR(200) NOT NULL,
    [Error] NVARCHAR(MAX) NULL
);
```

En proyectos reales probablemente agregues columnas extra (tenant, correlationId, etc.).  
Lo importante es que la tabla permita:

- Saber si un mensaje ya fue procesado (`ProcessedOnUtc` o un campo de estado).
- Guardar el payload serializado y el tipo.
- Relacionar el mensaje con un topic.

---

## 3. Registro de servicios

En `Program.cs` (hosting minimal) o `Startup.cs` registra los servicios del paquete.

### 3.1. API o Worker con Program.cs

```csharp
var builder = WebApplication.CreateBuilder(args);

// 1) Leer la seccion Kafka
var kafkaSection = builder.Configuration.GetSection("Kafka");

// 2) Registrar el broker de mensajes de Kafka
builder.Services.AddKafkaMessageBroker(kafkaSection);

// 3) Registrar hosted services para Outbox e Inbox
builder.Services.AddHostedService<OutboxDispatcher>();
builder.Services.AddHostedService<InboxProcessor>();

var app = builder.Build();
app.Run();
```

Ejemplo en Worker Service:

```csharp
IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        var kafkaSection = context.Configuration.GetSection("Kafka");

        services.AddKafkaMessageBroker(kafkaSection);
        services.AddHostedService<OutboxDispatcher>();
        services.AddHostedService<InboxProcessor>();
    })
    .Build();

await host.RunAsync();
```

Ajusta los nombres de tipos (`AddKafkaMessageBroker`, `OutboxDispatcher`, `InboxProcessor`) segun las clases reales del paquete.

---

## 4. Publicacion de eventos (lado Outbox)

Flujo del lado **productor**:

1. La aplicacion ejecuta logica de negocio dentro de una transaccion.
2. Actualiza sus tablas de dominio (Orders, Quotes, etc.).
3. En vez de llamar directamente a Kafka, agrega un registro en la tabla Outbox.
4. Se hace `COMMIT` de la transaccion.
5. Un worker (`OutboxDispatcher`) toma los mensajes pendientes y los envia a Kafka.

Ejemplo de servicio que usa un publisher de Outbox:

```csharp
public class QuoteService
{
    private readonly IUnitOfWork _uow;
    private readonly IOutboxPublisher _outbox;

    public QuoteService(IUnitOfWork uow, IOutboxPublisher outbox)
    {
        _uow = uow;
        _outbox = outbox;
    }

    public async Task ApproveQuoteAsync(Guid quoteId, CancellationToken ct = default)
    {
        Quote quote = await _uow.Quotes.GetByIdAsync(quoteId, ct);

        quote.Approve(); // logica de dominio

        var @event = new QuoteApprovedEvent
        {
            QuoteId = quote.Id,
            CustomerId = quote.CustomerId,
            ApprovedBy = quote.ApprovedBy,
            ApprovedOnUtc = DateTime.UtcNow
        };

        // Agregar a Outbox dentro de la misma transaccion
        await _outbox.AddAsync(
            topic: "quotes.approved",
            message: @event,
            ct: ct);

        await _uow.SaveChangesAsync(ct); // persiste Quote + Outbox de forma atomica
    }
}
```

Idea clave: no llamar a Kafka dentro de la transaccion. Solo se escribe en la tabla Outbox.

---

## 5. Dispatcher de Outbox

El paquete expone un hosted service que:

1. Lee periodicamente registros pendientes de `OutboxMessages`.
2. Publica cada mensaje al topic correspondiente en Kafka usando `IKafkaTransportPublisher`.
3. Marca el mensaje como procesado y registra errores si ocurren.

Configuracion tipica:

- Tamano de lote (cantidad de mensajes por ciclo).
- Intervalo de polling.
- Maximo de reintentos y delay entre reintentos para errores transitorios de Kafka.

Normalmente solo necesitas:

- Habilitar Outbox en `appsettings.json` (`"Outbox": { "Enabled": true }`).
- Registrar el hosted service en DI.

---

## 6. Consumo de eventos (lado Inbox)

Del lado **consumidor** el flujo es:

1. Un consumer de Kafka recibe el mensaje desde un topic.
2. Antes de procesarlo, lo guarda en la tabla Inbox dentro de una transaccion local.
3. Un worker de Inbox lee los mensajes pendientes, los deserializa y llama a los handlers de negocio.
4. Al completar el procesamiento, el mensaje se marca como procesado.

Ejemplo de handler:

```csharp
public class QuoteApprovedHandler : IInboxMessageHandler<QuoteApprovedEvent>
{
    private readonly IQuoteReadModel _readModel;

    public QuoteApprovedHandler(IQuoteReadModel readModel)
    {
        _readModel = readModel;
    }

    public async Task HandleAsync(QuoteApprovedEvent message, CancellationToken ct = default)
    {
        await _readModel.MarkAsApprovedAsync(message.QuoteId, ct);
    }
}
```

El paquete se encarga de:

- Deserializar `QuoteApprovedEvent` desde el registro en Inbox.
- Garantizar que cada mensaje se procese una sola vez (idempotencia).
- Manejar reintentos y errores.

---

## 7. Desarrollo local

Para desarrollo local normalmente se usa Kafka en Docker.

Ejemplo simple de `docker-compose.yml`:

```yaml
services:
  zookeeper:
    image: confluentinc/cp-zookeeper:7.7.0
    environment:
      ZOOKEEPER_CLIENT_PORT: 2181

  kafka:
    image: confluentinc/cp-kafka:7.7.0
    ports:
      - "9092:9092"
    environment:
      KAFKA_ZOOKEEPER_CONNECT: "zookeeper:2181"
      KAFKA_ADVERTISED_LISTENERS: "PLAINTEXT://localhost:9092"
      KAFKA_OFFSETS_TOPIC_REPLICATION_FACTOR: 1
```

Pasos tipicos:

1. Levantar Kafka con `docker compose up -d`.
2. Aplicar migraciones para crear las tablas Outbox e Inbox.
3. Ejecutar la API o worker.
4. Usar `kafka-console-consumer` o una UI (Kafka UI, Conduktor, etc.) para inspeccionar los topics.

---

## 8. Troubleshooting

### No se puede resolver IKafkaTransportPublisher

Verifica que el metodo de registro del paquete (`AddKafkaMessageBroker`) se ejecute antes de registrar los hosted services (`OutboxDispatcher`, etc.).

### No se estan enviando mensajes a Kafka

- Verificar que `Kafka:Outbox:Enabled` este en `true`.
- Confirmar que existan registros pendientes en la tabla Outbox.
- Verificar que `BootstrapServers` y la red sean correctos.

### Mensajes procesados mas de una vez

- Confirmar que la tabla Inbox se use y que el campo `ProcessedOnUtc` (o estado) se actualice.
- Verificar que la logica de los handlers sea idempotente (ejemplo: upserts, checks antes de insertar).
