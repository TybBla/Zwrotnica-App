using RefundPilot.Application.Abstractions;
using RefundPilot.Infrastructure.Email;

namespace RefundPilot.Tests.Infrastructure;

public sealed class FileEmailSenderTests : IDisposable
{
    private readonly string _outbox = Path.Combine(Path.GetTempPath(), $"zwrotnica-outbox-{Guid.NewGuid():N}");

    public void Dispose()
    {
        try { Directory.Delete(_outbox, recursive: true); } catch (IOException) { }
    }

    [Fact]
    public async Task WritesOneEmlFile_WithTextAndHtmlAlternatives()
    {
        var sender = new FileEmailSender(new EmailOptions { From = "no-reply@zwrotnica.test", FromName = "Zwrotnica" }, _outbox);

        await sender.SendAsync("anna@test.pl", "Czy przewoźnik odpowiedział?", "Dzień dobry, treść", "<p>Dzień dobry, treść</p>",
            CancellationToken.None);

        var file = Assert.Single(Directory.GetFiles(_outbox, "*.eml"));
        var eml = await File.ReadAllTextAsync(file);
        Assert.Contains("To: anna@test.pl", eml);
        Assert.Contains("no-reply@zwrotnica.test", eml);
        Assert.Contains("Zwrotnica", eml);
        Assert.Contains("multipart/alternative", eml);
        Assert.Contains("text/plain", eml);
        Assert.Contains("text/html", eml);
        Assert.Matches(@"(?m)^Subject: \S", eml);
    }

    [Fact]
    public async Task InvalidRecipient_Throws_AndWritesNothing()
    {
        var sender = new FileEmailSender(new EmailOptions(), _outbox);

        var error = await Assert.ThrowsAsync<EmailRecipientRejectedException>(() =>
            sender.SendAsync("not an address", "S", "T", "<p>T</p>", CancellationToken.None));
        Assert.Equal(EmailRejection.InvalidAddress, error.Reason);
        Assert.Empty(Directory.Exists(_outbox) ? Directory.GetFiles(_outbox) : []);
    }
}
