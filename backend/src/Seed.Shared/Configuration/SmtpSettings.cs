namespace Seed.Shared.Configuration;

public sealed class SmtpSettings
{
    public const string SectionName = "Smtp";

    public string Host { get; init; } = string.Empty;
    public int Port { get; init; } = 587;
    public string Username { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public string FromEmail { get; init; } = string.Empty;
    public string FromName { get; init; } = "Seed App";
    /// <summary>
    /// Connection security: "None", "StartTls", or "SslOnConnect".
    /// </summary>
    public string Security { get; init; } = "StartTls";
}
