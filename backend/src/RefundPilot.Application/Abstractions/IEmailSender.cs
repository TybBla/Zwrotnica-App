namespace RefundPilot.Application.Abstractions;

/// <summary>Sends one e-mail with a plain-text body and an HTML alternative. Throws when it could not be handed over.</summary>
public interface IEmailSender
{
    Task SendAsync(string to, string subject, string textBody, string htmlBody, CancellationToken ct);
}
