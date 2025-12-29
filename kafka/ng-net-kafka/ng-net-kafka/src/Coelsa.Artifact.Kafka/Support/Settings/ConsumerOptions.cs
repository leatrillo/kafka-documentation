namespace Coelsa.Artifact.Kafka.Support.Settings;
internal sealed class ConsumerOptions
{
    /// <summary>
    /// Id del grupo de consumidores.
    /// </summary>
    public string GroupId { get; set; } = string.Empty;

    /// <summary>
    /// Rutas de los servidores Kafka.
    /// </summary>
    public string BootstrapServers { get; init; } = string.Empty;

    /// <summary>
    /// Establece el comportamiento de restablecimiento automático del offset.
    /// </summary>
    public AutoOffsetReset AutoOffsetReset { get; init; } = AutoOffsetReset.Earliest;

    /// <summary>
    /// Establece si se debe habilitar la confirmación automática de desplazamientos.
    /// </summary>
    public bool EnableAutoCommit { get; init; } = false;

    /// <summary>
    /// Tiempo de espera de sesión para el consumidor
    /// </summary>
    public TimeSpan SessionTimeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Intervalo entre Heartbeats.
    /// </summary>
    public TimeSpan HeartbeatInterval { get; init; } = TimeSpan.FromSeconds(3);

    /// <summary>
    /// tiempo máximo de espera para los mensajes.
    /// </summary>
    public TimeSpan ConsumeTimeout { get; init; } = TimeSpan.FromMilliseconds(100000);

    /// <summary>
    /// Número máximo de mensajes a recuperar por solicitud.
    /// </summary>
    public int MaxPollRecords { get; init; } = 500;

    /// <summary>
    /// Número mínimo de bytes a recuperar.
    /// </summary>
    public int FetchMinBytes { get; init; } = 1;

    /// <summary>
    /// Tiempo máximo de espera para las solicitudes de obtención de datos.
    /// </summary>
    public TimeSpan FetchMaxWait { get; init; } = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// Habilitar las notificaciones de fin de archivo de partición.
    /// </summary>
    public bool EnablePartitionEof { get; init; } = false;

    /// <summary>
    /// Número máximo de procesadores de mensajes concurrentes.
    /// </summary>
    public int MaxConcurrency { get; init; } = 1;
}
