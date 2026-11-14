using System.Net.Mail;
using RefundPilot.Application.Abstractions;
using RefundPilot.Infrastructure.Email;

namespace RefundPilot.Tests.Infrastructure;

public sealed class SmtpEmailSenderTests
{
    // Nothing listens on port 1: a connection attempt fails at once, without any network.
    private static SmtpEmailSender Sender() =>
        new(new EmailOptions { Smtp = new SmtpOptions { Host = "127.0.0.1", Port = 1, EnableSsl = false } });

    [Theory]
    [InlineData("demo@zwrotnica.test")]
    [InlineData("a@example.com")]
    [InlineData("a@mail.example.org")]
    [InlineData("a@host.invalid")]
    [InlineData("a@app.localhost")]
    [InlineData("a@shop.example")]
    public async Task ReservedDomains_AreNeverHandedToSmtp(string to)
    {
        var error = await Assert.ThrowsAsync<EmailRecipientRejectedException>(() =>
            Sender().SendAsync(to, "S", "T", "<p>T</p>", CancellationToken.None));

        Assert.Equal(EmailRejection.ReservedDomain, error.Reason);
    }

    [Fact]
    public async Task InvalidAddress_IsAPermanentRejection()
    {
        var error = await Assert.ThrowsAsync<EmailRecipientRejectedException>(() =>
            Sender().SendAsync("not an address", "S", "T", "<p>T</p>", CancellationToken.None));

        Assert.Equal(EmailRejection.InvalidAddress, error.Reason);
    }

    [Fact]
    public async Task UnreachableServer_IsATransportFailure()
    {
        await Assert.ThrowsAsync<EmailTransportException>(() =>
            Sender().SendAsync("jan@realdomain.pl", "S", "T", "<p>T</p>", CancellationToken.None));
    }

    [Theory]
    [InlineData(SmtpStatusCode.MailboxUnavailable)]
    [InlineData(SmtpStatusCode.MailboxNameNotAllowed)]
    [InlineData(SmtpStatusCode.UserNotLocalTryAlternatePath)]
    public void RecipientRefusedWith5xx_IsPermanent(SmtpStatusCode code)
    {
        var error = Assert.IsType<EmailRecipientRejectedException>(
            SmtpEmailSender.Translate(new SmtpFailedRecipientException(code, "x@y.pl")));

        Assert.Equal(EmailRejection.RecipientRefused, error.Reason);
    }

    [Fact]
    public void BusyMailbox_StaysATransientFailureOfThatMessage()
    {
        var original = new SmtpFailedRecipientException(SmtpStatusCode.MailboxBusy, "x@y.pl");

        Assert.Same(original, SmtpEmailSender.Translate(original));
    }

    [Fact]
    public void OtherSmtpErrors_AreTransportFailures()
    {
        Assert.IsType<EmailTransportException>(SmtpEmailSender.Translate(new SmtpException(SmtpStatusCode.GeneralFailure)));
    }
}
