using Coelsa.Artifact.Kafka.Support.Settings;
using Microsoft.Data.SqlClient;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Coelsa.Artifact.Kafka.Support;
public static partial class CoelsaKafkaTools
{
    private static readonly JsonSerializerOptions jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,

        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,

        WriteIndented = false,

        PropertyNameCaseInsensitive = true,

        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static string JsonSerialize<T>(T obj) where T : class
    {
        ArgumentNullException.ThrowIfNull(obj);

        try
        {
            return JsonSerializer.Serialize(obj, jsonOptions);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to serialize object of type '{typeof(T).Name}': {ex.Message}", ex);
        }
    }

    public static T? JsonDeserialize<T>(string message) where T : class
    {
        ArgumentNullException.ThrowIfNullOrWhiteSpace(message);

        try
        {
            return JsonSerializer.Deserialize<T>(message, jsonOptions);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to Deserialize object of type '{typeof(T).Name}': {ex.Message}", ex);
        }
    }

    internal static KafkaOptions AddSettings(IServiceCollection services, IConfiguration configuration)
    {
        IConfigurationSection sectionConfig = configuration.GetSection(nameof(KafkaOptions));

        services.Configure<KafkaOptions>(sectionConfig);

        KafkaOptions options = sectionConfig.Get<KafkaOptions>() ?? throw new ArgumentException($"No se encuentran los parametros de configuracion, verifique que está la seccion '{nameof(KafkaOptions)}'");

        SanitizeAndQuoteSqlIdentifier(options.SqlServer.Initials, nameof(options.SqlServer.Initials), 8);

        return options;
    }

    /// <summary>
    /// Sanitiza y escapa un identificador SQL usando corchetes para prevenir inyección SQL.
    /// </summary>
    /// <param name="identifier">El identificador a sanitizar</param>
    /// <param name="parameterName">Nombre del parámetro para mensajes de error</param>
    /// <returns>Identificador escapado con corchetes [identifier]</returns>
    private static void SanitizeAndQuoteSqlIdentifier(string identifier, string parameterName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(identifier))
            throw new ArgumentException($"{parameterName} cannot be null or empty.", parameterName);

        // Validar formato: solo letras y guión bajo
        // Debe comenzar con letra o guión bajo
        if (!SqlSanitize().IsMatch(identifier))
            throw new ArgumentException($"{parameterName} '{identifier}' contains invalid characters. Only letters, numbers, and underscores are allowed, and it must start with a letter or underscore.", parameterName);

        // Validar longitud máxima 
        if (identifier.Length > maxLength)
            throw new ArgumentException($"{parameterName} '{identifier}' exceeds maximum length of {maxLength} characters.", parameterName);

        // Validar contra palabras reservadas de SQL Server
        ValidateNotReservedWord(identifier, parameterName);
    }

    /// <summary>
    /// Valida que el identificador no sea una palabra reservada de SQL Server.
    /// </summary>
    private static void ValidateNotReservedWord(string identifier, string parameterName)
    {
        // Lista de palabras reservadas críticas de SQL Server
        HashSet<string> reservedWords = new(StringComparer.OrdinalIgnoreCase)
        {
            "SELECT", "INSERT", "UPDATE", "DELETE", "DROP", "CREATE", "ALTER",
            "EXEC", "EXECUTE", "DECLARE", "SET", "BEGIN", "END", "TRANSACTION",
            "COMMIT", "ROLLBACK", "TRUNCATE", "GRANT", "REVOKE", "DENY",
            "BACKUP", "RESTORE", "SHUTDOWN", "KILL", "TABLE", "DATABASE",
            "SCHEMA", "INDEX", "VIEW", "PROCEDURE", "FUNCTION", "TRIGGER",
            "USER", "LOGIN", "ROLE", "PARTITION", "CONSTRAINT"
        };

        if (reservedWords.Contains(identifier))
            throw new ArgumentException($"{parameterName} '{identifier}' is a SQL Server reserved word and cannot be used.", parameterName);
    }

    [GeneratedRegex(@"^[a-zA-Z_][a-zA-Z_]*$")]
    private static partial Regex SqlSanitize();

    /// <summary>
    /// Determina si un error de SQL es transitorio y puede ser reintentado.
    /// </summary>
    internal static bool IsTransientError(SqlException sqlException)
    {
        // Lista de códigos de error transitorios de SQL Server
        // https://docs.microsoft.com/en-us/azure/sql-database/sql-database-develop-error-messages
        HashSet<int> transientErrorNumbers =
        [
            -2,     // Timeout
            -1,     // Connection broken
            2,      // Network error
            53,     // Connection broken
            64,     // Error not handled by SqlException
            233,    // Connection initialization error
            10053,  // Transport-level error (broken connection)
            10054,  // Transport-level error (forced close)
            10060,  // Network or instance-specific error
            10061,  // Network or instance-specific error
            10928,  // Resource limit reached
            10929,  // Resource limit reached
            40197,  // Service error processing request
            40501,  // Service is busy
            40613,  // Database unavailable
            49918,  // Cannot process request (insufficient resources)
            49919,  // Cannot process create or update request
            49920,  // Cannot process request (too many operations)
            4060,   // Cannot open database
            4221,   // Login timeout
            18401,  // Login failed
            20,     // Instance not found
            17197,  // Login timeout
            17142,  // Too many connections
            1205,   // Deadlock victim
            1222    // Lock request timeout
        ];

        return transientErrorNumbers.Contains(sqlException.Number);
    }


    /// <summary>
    /// Calcula el delay para el siguiente reintento usando backoff exponencial con jitter.
    /// </summary>
    internal static TimeSpan CalculateRetryDelay(int attempt, TimeSpan baseDelay)
    {
        // Backoff exponencial: baseDelay * 2^(attempt-1)
        double exponentialDelay = baseDelay.TotalMilliseconds * Math.Pow(2, attempt - 2);

        // Limitar el delay máximo a 30 segundos
        exponentialDelay = Math.Min(exponentialDelay, 30000);

        // Agregar jitter (±20%) para evitar thundering herd
        double jitter = Random.Shared.NextDouble() * 0.4 - 0.2; // -20% a +20%
        double finalDelay = exponentialDelay * (1 + jitter);

        return TimeSpan.FromMilliseconds(Math.Max(finalDelay, baseDelay.TotalMilliseconds));
    }
}
