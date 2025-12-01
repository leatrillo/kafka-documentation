# Coelsa.Artifact.MessageBroker.Kafka

Integracion con Apache Kafka para servicios .NET usando el patron Outbox / Inbox.

Este paquete apunta a resolver dos problemas clasicos:

- Publicar eventos de dominio de forma confiable en Kafka (sin perder mensajes).
- Consumir eventos de forma idempotente (sin efectos duplicados) usando Inbox.

La libreria expone:

- Integracion con Kafka basada en Confluent.Kafka.
- Patron Outbox del lado productor (API / servicios).
- Patron Inbox del lado consumidor (workers).
- Hosted services para despachar Outbox y procesar Inbox.
- Interfaces simples para publicar y manejar eventos de dominio.

> Nota: los nombres de tipos y metodos que aparecen aca son un ejemplo de uso.  
> Ajustalos a los nombres reales del paquete segun tu codigo.

---

## 1. Instalacion

Desde la raiz del proyecto donde quieras usar el paquete:

```bash
dotnet add package Coelsa.Artifact.MessageBroker.Kafka
```

O agregalo desde el administrador de paquetes de NuGet en Visual Studio.

---

## 2. Requisitos

- .NET (por ejemplo net6.0, net7.0 o net8.0, segun el target del paquete).
- Una base de datos relacional para almacenar Outbox e Inbox  
  (ejemplo: SQL Server, PostgreSQL, etc).
- Un cluster de Kafka accesible desde los servicios (local con Docker o remoto).

---

## 3. Configuracion basica

### 3.1. Seccion Kafka en appsettings.json

Agrega en `appsettings.json` algo similar a:

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
      "PollingIntervalSeconds": 5,
      "BatchSize": 100
    },

    "Inbox": {
      "Enabled": true,
      "PollingIntervalSeconds": 5,
      "BatchSize": 100
    }
  }
}
```

Campos tipicos:

- `BootstrapServers`: endpoint de Kafka, por ejemplo `localhost:9092` o `<host>:9092`.
- `Consumer.GroupId`: identificador de grupo del consumer (uno por servicio worker).
- `Outbox` / `Inbox`: switches y parametros para los workers (intervalo, tamanio de lote, etc).

Ajusta los nombres y propiedades a lo que el paquete realmente expone (por ejemplo si usa otras claves o sub secciones).

---

### 3.2. Tablas Outbox e Inbox

El paquete asume tablas para Outbox e Inbox en la base de datos.

Ejemplo simple de Outbox:

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

Ejemplo simple de Inbox:

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

En tu implementacion real podes:

- Agregar columnas de trazabilidad (correlation id, tenant, etc).
- Cambiar los nombres de tablas y columnas segun tu esquema.

Lo importante es:

- Tener una marca de procesado (`ProcessedOnUtc` o estado).
- Poder guardar el payload serializado y el tipo.
- Saber a que topic pertenece el mensaje (o de donde vino).

---

## 4. Registro en DI

### 4.1. API (productor) con Program.cs

En un proyecto API o servicio que publica eventos:

```csharp
var builder = WebApplication.CreateBuilder(args);

// Leer seccion Kafka
var kafkaSection = builder.Configuration.GetSection("Kafka");

// Registrar servicios del broker de mensajes
builder.Services.AddKafkaMessageBroker(kafkaSection);

// Registrar hosted service para Outbox (si aplica en este proyecto)
builder.Services.AddHostedService<OutboxDispatcher>();

var app = builder.Build();
app.MapControllers();
app.Run();
```

### 4.2. Worker (consumidor) con Host

En un proyecto Worker que consume eventos (Inbox):

```csharp
IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        var kafkaSection = context.Configuration.GetSection("Kafka");

        services.AddKafkaMessageBroker(kafkaSection);

        // Worker que lee desde Kafka y llena la Inbox
        services.AddHostedService<KafkaConsumerWorker>();

        // Worker que procesa la tabla Inbox
        services.AddHostedService<InboxProcessor>();
    })
    .Build();

await host.RunAsync();
```

> Nota: usa los nombres reales de tus clases de hosted services  
> (por ejemplo `OutboxDispatcher`, `InboxProcessor`, `KafkaConsumerWorker`, etc).

---

## 5. Uso como productor (Outbox)

Flujo tipico desde un servicio de aplicacion:

1. Ejecutar logica de negocio dentro de una transaccion.
2. Guardar cambios en las tablas de dominio.
3. Crear un evento de dominio.
4. En lugar de llamar a Kafka, agregar el evento a la Outbox.
5. Confirmar la transaccion (persistir dominio + Outbox).
6. Un worker aparte toma los registros de Outbox y los publica en Kafka.

Ejemplo:

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
        // 1) Cargar entidad
        Quote quote = await _uow.Quotes.GetByIdAsync(quoteId, ct);

        // 2) Logica de dominio
        quote.Approve();

        // 3) Crear evento de dominio
        var @event = new QuoteApprovedEvent
        {
            QuoteId = quote.Id,
            CustomerId = quote.CustomerId,
            ApprovedBy = quote.ApprovedBy,
            ApprovedOnUtc = DateTime.UtcNow
        };

        // 4) Agregar a Outbox (no llamar a Kafka directo)
        await _outbox.AddAsync(
            topic: "quotes.approved",
            message: @event,
            ct: ct);

        // 5) Confirmar cambios (dominio + Outbox)
        await _uow.SaveChangesAsync(ct);
    }
}
```

El hosted service de Outbox:

- Lee filas pendientes de `OutboxMessages`.
- Publica cada mensaje al topic correspondiente usando el publisher de Kafka.
- Marca la fila como procesada y guarda el error si algo falla.

---

## 6. Uso como consumidor (Inbox)

Del lado consumidor el flujo tipico es:

1. Un consumer de Kafka recibe mensajes de uno o varios topics.
2. Cada mensaje se persiste en la tabla Inbox (una fila por mensaje).
3. Un worker lee la Inbox y ejecuta el handler correspondiente al tipo de mensaje.
4. Si el handler termina bien, el mensaje se marca como procesado.

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
        // Actualizar proyecciones, enviar notificaciones, etc
        await _readModel.MarkAsApprovedAsync(message.QuoteId, ct);
    }
}
```

El paquete se encarga de:

- Deserializar el payload desde la fila de Inbox al tipo `QuoteApprovedEvent`.
- Verificar si el mensaje ya fue procesado.
- Manejar reintentos segun la configuracion.

---

## 7. Desarrollo local con Docker

Para desarrollo local, lo mas simple es levantar Kafka con Docker.

Ejemplo basico de `docker-compose.yml`:

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

1. Ejecutar `docker compose up -d`.
2. Ejecutar migraciones para crear tablas Outbox e Inbox.
3. Levantar la API (productor).
4. Levantar el worker (consumidor).
5. Usar herramientas como `kafka-console-consumer` o una UI (Kafka UI, Conduktor, etc) para revisar los topics.

---

## 8. Problemas comunes

### No se puede resolver la interfaz del publisher de Kafka

Sintoma: error del tipo:

> Unable to resolve service for type `IKafkaTransportPublisher` al intentar activar `OutboxDispatcher`.

Verificar:

- Que el metodo de registro de este paquete (`AddKafkaMessageBroker` o similar) se llame antes de registrar los hosted services.
- Que no falte ningun proyecto de infraestructura en las referencias.

### No se estan enviando mensajes a Kafka

Revisar:

- Configuracion `Kafka:Outbox:Enabled = true`.
- Que existan filas pendientes en la tabla Outbox.
- Valores de `BootstrapServers` y conectividad a Kafka.
- Logs del hosted service de Outbox para ver errores.

### Mensajes procesados mas de una vez

Revisar:

- Que la tabla Inbox se actualice correctamente (campo de procesado).
- Que los handlers sean idempotentes (repetir el mismo evento no debe romper nada).
- Que no haya dos procesos diferentes leyendo la misma Inbox sin coordinacion (por ejemplo dos workers identicos sobre la misma tabla sin manejo de concurrencia).

---

## 9. Resumen

Este paquete encapsula la integracion con Kafka usando Outbox e Inbox, para que los servicios:

- No llamen a Kafka dentro de la transaccion de negocio.
- No pierdan eventos si Kafka esta caido.
- Puedan consumir mensajes de forma segura e idempotente.

La idea es que la logica de dominio se mantenga simple, y que toda la complejidad de mensajeria y confiabilidad quede centralizada en este paquete.
