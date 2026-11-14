using System.Net.Mail;
using RefundPilot.Application.Abstractions;

namespace RefundPilot.Infrastructure.Email;

/// <summary>
/// Used when no SMTP host is configured: each message becomes an .eml file in the outbox folder (open it in any
/// mail client), so reminders can be seen locally without an account anywhere.
/// </summary>
public sealed class FileEmailSender(EmailOptions options, string outboxPath) : IEmailSender
{
    public async Task SendAsync(string to, string subject, string textBody, string htmlBody, CancellationToken ct)
    {
        using var message = MailMessages.Create(options, to, subject, textBody, htmlBody);
        Directory.CreateDirectory(outboxPath);
        using var client = new SmtpClient
        {
            DeliveryMethod = SmtpDeliveryMethod.SpecifiedPickupDirectory,
            PickupDirectoryLocation = Path.GetFullPath(outboxPath),
        };
        await client.SendMailAsync(message, ct);
    }
}
