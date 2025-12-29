using Coelsa.Artifact.Kafka.Model;
using Coelsa.Artifact.Kafka.Model.Enum;
using System.Data;

namespace Coelsa.Artifact.Kafka.Handler.Interfaces;

public interface ICoelsaKafkaProducer
{
    /// <summary>
    /// Publica un mensaje en Kafka de forma asíncrona. En el caso de que no pueda guardar el mensaje devuelve el PersistenceStatus o lanza una excepción.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="configuration"></param>
    /// <param name="message"></param>
    /// <param name="cancellationToken"></param>
    /// <remarks><strong>Advertencia:</strong> Es de responsabilidad de la aplicación que utiliza el paquete manejar los errores en el caso de que no se pueda enviar el mensaje a kafka.</remarks>
    /// <returns></returns>
    Task<CoelsaPersistenceStatus> PublishAsync<T>(MessageConfiguration configuration, CoelsaMessage<T> message, CancellationToken cancellationToken = default) where T : class;
    /// <summary>
    /// Publica un mensaje en Kafka de forma asíncrona. En el caso de que no pueda guardar el mensaje intenta guardar en la base de datos de la aplicación en la tabla OUTBOX_MESSAGES si no logra devuelve PersistenceStatus o lanza una excepción.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="configuration"></param>
    /// <param name="message"></param>
    /// <param name="cancellationToken"></param>
    /// <remarks><strong>Advertencia:</strong> Es de responsabilidad de la aplicación que utiliza el paquete manejar los errores en el caso de que no se pueda enviar el mensaje a kafka.</remarks>
    /// <returns></returns>
    Task<CoelsaPersistenceStatus> PublishOutboxAsync<T>(MessageConfiguration configuration, CoelsaMessage<T> message, IDbConnection connection, IDbTransaction? transaction = null, CancellationToken cancellationToken = default) where T : class;
}
