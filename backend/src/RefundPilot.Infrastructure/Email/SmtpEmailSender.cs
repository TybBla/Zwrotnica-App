using System.Net;
using System.Net.Mail;
using RefundPilot.Application.Abstractions;

namespace RefundPilot.Infrastructure.Email;

/// <summary>Sends through any SMTP relay with STARTTLS (e.g. Brevo, or Gmail with an app password).</summary>
public sealed class SmtpEmailSender(EmailOptions options) : IEmailSender
{
    public async Task SendAsync(string to, string subject, string textBody, string htmlBody, CancellationToken ct)
    {
        using var message = MailMessages.Create(options, to, subject, textBody, htmlBody);
        using var client = new SmtpClient(options.Smtp.Host, options.Smtp.Port) { EnableSsl = options.Smtp.EnableSsl };
        if (!string.IsNullOrEmpty(options.Smtp.User))
            client.Credentials = new NetworkCredential(options.Smtp.User, options.Smtp.Password);
        await client.SendMailAsync(message, ct);
    }
}
