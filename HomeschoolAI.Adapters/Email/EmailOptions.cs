namespace HomeschoolAI.Adapters.Email;

/// <summary>
/// Configuración del servidor SMTP (Mailpit en desarrollo).
/// </summary>
public sealed class EmailOptions
{
    public string Host { get; set; } = "localhost";

    public int Port { get; set; } = 1025;

    public string From { get; set; } = "no-reply@homeschool.local";
}
