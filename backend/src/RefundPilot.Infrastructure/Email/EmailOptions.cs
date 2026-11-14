namespace RefundPilot.Infrastructure.Email;

/// <summary>Email:* settings. Credentials only ever come from app settings / environment, never from the repo.</summary>
public sealed class EmailOptions
{
    public string From { get; init; } = "no-reply@zwrotnica.test";
    public string FromName { get; init; } = "Zwrotnica";
    public SmtpOptions Smtp { get; init; } = new();
}

public sealed class SmtpOptions
{
    public string? Host { get; init; }
    /// <summary>587 with STARTTLS: System.Net.Mail does not support implicit TLS on 465.</summary>
    public int Port { get; init; } = 587;
    public string? User { get; init; }
    public string? Password { get; init; }
    public bool EnableSsl { get; init; } = true;
}
