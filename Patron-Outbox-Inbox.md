# Patron Outbox / Inbox

El patron **Outbox / Inbox** resuelve el problema de consistencia entre la base de datos de la aplicacion y el sistema de mensajeria (Kafka).

Sin este patron, si intentamos guardar datos y enviar un mensaje a Kafka al mismo tiempo, podemos terminar en un estado inconsistente:

- Se guarda en la base de datos pero falla el envio a Kafka.
- Se envia a Kafka pero falla el guardado en la base (`SaveChanges`).

Con Outbox / Inbox podemos lograr:

- Que lo que ocurre en la base de datos se refleje en Kafka al menos una vez.
- Que los consumidores procesen los mensajes de forma idempotente (no hay efectos duplicados).

---

## Outbox (lado productor)

En el servicio productor el flujo es:

1. La operacion de negocio se ejecuta dentro de una transaccion de base de datos.
2. La transaccion actualiza las tablas de dominio (por ejemplo `Quotes`, `Orders`, etc.).
3. En lugar de llamar directamente a Kafka, se inserta un registro en la tabla Outbox con:
   - Tipo de evento (por ejemplo `QuoteApprovedEvent`).
   - Payload serializado (JSON, Avro, etc.).
   - Topic de destino.
4. Se hace `COMMIT` de la transaccion.

Un worker de Outbox:

1. Lee periodicamente los registros pendientes en la tabla Outbox.
2. Publica cada mensaje en el topic correspondiente en Kafka.
3. Marca los registros como procesados (campo `ProcessedOnUtc` o similar).
4. Registra informacion de error en caso de fallo y permite reintentos controlados.

### Beneficios del Outbox

- Consistencia: la escritura de datos y la creacion del mensaje se hace en la misma transaccion.
- Resiliencia: si Kafka esta caido, los mensajes quedan almacenados en Outbox y se reenvian despues.
- Auditoria y debug: se puede revisar historico de mensajes generados y su estado.

---

## Inbox (lado consumidor)

En el servicio consumidor el flujo es:

1. Un consumer de Kafka recibe el mensaje desde un topic.
2. Antes de ejecutar la logica de negocio, se guarda el mensaje en la tabla Inbox (una fila por mensaje) dentro de una transaccion local.
3. Un worker de Inbox lee los mensajes pendientes, deserializa el payload y llama al handler de dominio correspondiente.
4. Al finalizar el procesamiento, el mensaje se marca como procesado (por ejemplo, se setea `ProcessedOnUtc`).

Si el proceso se cae despues de guardar en Inbox pero antes de completar la logica de negocio, el mensaje sigue estando en la tabla y puede reintentarse sin perderlo.

### Beneficios del Inbox

- Idempotencia: cada mensaje tiene un identificador unico y la tabla Inbox permite saber si ya fue procesado.
- Backpressure: es posible controlar el ritmo de procesamiento a nivel base de datos (por ejemplo, por lotes).
- Reprocesamiento: se pueden reintentar mensajes fallidos sin depender de mover offsets manualmente en Kafka.

---

## Outbox + Inbox + Kafka

Usando Outbox e Inbox en conjunto con Kafka:

- El productor nunca pierde los eventos de dominio, aun si Kafka no esta disponible temporalmente.
- El consumidor puede reintentar procesamiento sin causar efectos duplicados en el dominio.
- Se habilita una integracion confiable entre servicios independientes (cada uno con su propia base y tecnologia).

Este enfoque es especialmente util en arquitecturas de microservicios donde cada servicio necesita publicar y consumir eventos de manera robusta y trazable.
