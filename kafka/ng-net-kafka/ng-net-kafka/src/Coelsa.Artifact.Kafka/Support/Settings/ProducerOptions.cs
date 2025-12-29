namespace Coelsa.Artifact.Kafka.Support.Settings;

internal sealed class ProducerOptions
{
    /// <summary>
    /// Rutas de los servidores Kafka.
    /// </summary>
    public string BootstrapServers { get; init; } = string.Empty;

    /// <summary>
    /// Nivel de acuse de recibo requerido por el productor.
    /// </summary>
    public Acks Acks { get; init; } = Acks.All;

    public bool EnableIdempotence { get; init; } = true;

    /// <summary>
    /// Tiempo máximo de espera para una respuesta
    /// </summary>
    public TimeSpan RequestTimeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Tiempo máximo de bloqueo en espera de espacio en el búfer.
    /// </summary>
    public TimeSpan MaxBlockMs { get; init; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Tamaño del lote para el procesamiento por lotes de mensajes.
    /// </summary>
    public int BatchSize { get; init; } = 16384;

    /// <summary>
    /// Tiempo de espera para recibir mensajes adicionales.
    /// </summary>
    public TimeSpan LingerMs { get; init; } = TimeSpan.FromMilliseconds(5);

    /// <summary>
    /// Tipo de compresión para los mensajes.
    /// </summary>
    public CompressionType CompressionType { get; init; } = CompressionType.None;

    /// <summary>
    /// Obtiene o establece el número máximo de solicitudes no reconocidas.
    /// </summary>
    public int MaxInFlight { get; init; } = 5;

    /// <summary>
    /// Obtiene o establece el número de reintentos para envíos fallidos.
    /// </summary>
    public int Retries { get; init; } = 5;

    /// <summary>
    /// Obtiene o establece el tiempo de espera para reintentos.
    /// </summary>
    public TimeSpan RetryBackoffMs { get; init; } = TimeSpan.FromMilliseconds(100);
}
