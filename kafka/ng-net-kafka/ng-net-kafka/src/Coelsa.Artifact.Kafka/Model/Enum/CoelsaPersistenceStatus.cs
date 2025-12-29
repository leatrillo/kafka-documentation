namespace Coelsa.Artifact.Kafka.Model.Enum;

public enum CoelsaPersistenceStatus
{
    //
    // Resumen:
    //     Message was never transmitted to the broker, or failed with an error indicating
    //     it was not written to the log. Application retry risks ordering, but not duplication.
    NotPersisted,
    //
    // Resumen:
    //     Message was transmitted to broker, but no acknowledgement was received. Application
    //     retry risks ordering and duplication.
    PossiblyPersisted,
    //
    // Resumen:
    //     Message was written to the log and acknowledged by the broker. Note: acks='all'
    //     should be used for this to be fully trusted in case of a broker failover.
    Persisted
}
