using System.Net;
using System.Net.Mail;
using RefundPilot.Application.Abstractions;

namespace RefundPilot.Infrastructure.Email;

/// <summary>Sends through any SMTP relay with STARTTLS (e.g. Brevo, or Gmail with an app password).</summary>
public sealed class SmtpEmailSender(EmailOptions options) : IEmailSender
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    /// <summary>RFC 2606 / 6761 names: they never receive mail, so a real relay is not even asked (e.g. the demo account).</summary>
    private static readonly string[] ReservedSuffixes = [".test", ".example", ".invalid", ".localhost"];
    private static readonly string[] ReservedDomains = ["example.com", "example.net", "example.org", "localhost"];

    public async Task SendAsync(string to, string subject, string textBody, string htmlBody, CancellationToken ct)
    {
        using var message = MailMessages.Create(options, to, subject, textBody, htmlBody);
        if (IsReserved(message.To[0].Host))
            throw new EmailRecipientRejectedException(EmailRejection.ReservedDomain);

        using var client = new SmtpClient(options.Smtp.Host, options.Smtp.Port)
        {
            EnableSsl = options.Smtp.EnableSsl,
            Timeout = (int)Timeout.TotalMilliseconds,
        };
        if (!string.IsNullOrEmpty(options.Smtp.User))
            client.Credentials = new NetworkCredential(options.Smtp.User, options.Smtp.Password);

        // SmtpClient.Timeout covers only synchronous sends; the async path needs its own deadline.
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(Timeout);
        try
        {
            await client.SendMailAsync(message, deadline.Token);
        }
        catch (SmtpException ex)
        {
            throw Translate(ex);
        }
        catch (OperationCanceledException ex) when (!ct.IsCancellationRequested)
        {
            throw new EmailTransportException("The SMTP server did not answer in time.", ex);
        }
    }

    /// <summary>
    /// 550/551/553 for the recipient: permanent rejection. Other recipient errors (e.g. mailbox busy) stay a temporary
    /// failure of that message. Anything else (connection, TLS, authentication) means the server cannot be used now.
    /// </summary>
    public static Exception Translate(SmtpException ex) => ex switch
    {
        SmtpFailedRecipientException
        {
            StatusCode: SmtpStatusCode.MailboxUnavailable or SmtpStatusCode.MailboxNameNotAllowed
                or SmtpStatusCode.UserNotLocalTryAlternatePath,
        } => new EmailRecipientRejectedException(EmailRejection.RecipientRefused, ex),
        SmtpFailedRecipientException => ex,
        _ => new EmailTransportException($"SMTP failed ({ex.StatusCode}).", ex),
    };

    private static bool IsReserved(string host)
    {
        var domain = host.TrimEnd('.').ToLowerInvariant();
        return ReservedSuffixes.Any(domain.EndsWith)
               || ReservedDomains.Any(d => domain == d || domain.EndsWith("." + d));
    }
}
