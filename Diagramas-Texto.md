# Textos para diagramas de Outbox / Inbox con Kafka

A continuacion hay textos cortos para pegar en los bloques de los diagramas (por ejemplo en Azure DevOps Wiki, draw.io, PowerPoint, etc.).

---

## 1. Bloque: API / Servicio de negocio (productor)

> **Application Service / API**  
> Ejecuta logica de negocio y escribe en la base de datos.  
> En lugar de publicar directamente en Kafka, agrega mensajes a la tabla Outbox dentro de la misma transaccion.

---

## 2. Bloque: Tabla Outbox

> **Outbox table**  
> Tabla que almacena eventos pendientes de publicar.  
> Cada fila representa un mensaje a enviar a Kafka (tipo, payload, topic, estado y timestamps).

---

## 3. Bloque: Outbox Dispatcher (worker)

> **Outbox dispatcher**  
> Worker en background que lee mensajes pendientes de la tabla Outbox y los publica en Kafka.  
> Maneja reintentos, marca los mensajes como procesados y registra errores en caso de fallos.

---

## 4. Bloque: Kafka Cluster / Topics

> **Kafka cluster**  
> Sistema de mensajeria distribuido que recibe mensajes desde los productores y los persiste en topics particionados.  
> Diferentes servicios pueden suscribirse a estos topics para reaccionar a los eventos.

---

## 5. Bloque: Worker / Consumer Service

> **Consumer service / Worker**  
> Servicio que se suscribe a uno o mas topics de Kafka.  
> Recibe mensajes, los almacena en la tabla Inbox y ejecuta la logica de negocio correspondiente a traves de handlers.

---

## 6. Bloque: Tabla Inbox

> **Inbox table**  
> Tabla que registra los mensajes recibidos desde Kafka.  
> Se usa para garantizar procesamiento idempotente (cada mensaje se procesa solo una vez) y soportar reintentos controlados.

---

## 7. Bloque: Inbox Processor (worker)

> **Inbox processor**  
> Worker en background que lee mensajes pendientes de la tabla Inbox y ejecuta los handlers de dominio.  
> Al finalizar marca los mensajes como procesados para evitar ejecuciones duplicadas.

---

## 8. Bloque: Domain Handler / Application Logic

> **Domain / Application handler**  
> Componente que contiene la logica de negocio que se dispara al recibir un evento (por ejemplo actualizar un read-model o enviar notificaciones).  
> Debe ser idempotente para tolerar reintentos y posibles duplicados en el consumo.
