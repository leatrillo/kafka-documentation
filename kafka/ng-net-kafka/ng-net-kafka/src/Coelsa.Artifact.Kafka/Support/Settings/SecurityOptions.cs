namespace Coelsa.Artifact.Kafka.Support.Settings;
internal sealed class SecurityOptions
{
    /// <summary>
    /// Gets or sets the SASL username.
    /// </summary>
    public string Username { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the SASL password.
    /// </summary>
    public string Password { get; init; } = string.Empty;
}
