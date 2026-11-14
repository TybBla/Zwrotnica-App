using System.Net.Mail;
using System.Net.Mime;
using System.Text;

namespace RefundPilot.Infrastructure.Email;

internal static class MailMessages
{
    /// <summary>multipart/alternative: plain text first, the HTML version as the preferred alternative.</summary>
    public static MailMessage Create(EmailOptions options, string to, string subject, string textBody, string htmlBody)
    {
        var message = new MailMessage
        {
            From = new MailAddress(options.From, options.FromName, Encoding.UTF8),
            Subject = subject,
            SubjectEncoding = Encoding.UTF8,
            HeadersEncoding = Encoding.UTF8,
        };
        message.To.Add(new MailAddress(to));
        message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(textBody, Encoding.UTF8, MediaTypeNames.Text.Plain));
        message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(htmlBody, Encoding.UTF8, MediaTypeNames.Text.Html));
        return message;
    }
}
