using Microsoft.Extensions.Configuration;
using RefundPilot.Api;

namespace RefundPilot.Tests.Api;

public sealed class ReminderSettingsTests
{
    private const string Smtp = "smtp-relay.brevo.com";
    private const string Url = "https://zwrotnica-demo.azurewebsites.net";

    private static ReminderSettings Resolve(bool development, string? url = null, string? smtpHost = null, string? enabled = null) =>
        ReminderSettings.From(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["PublicBaseUrl"] = url,
            ["Email:Smtp:Host"] = smtpHost,
            ["Reminders:Enabled"] = enabled,
        }).Build(), development);

    [Fact]
    public void Development_RunsWithTheLocalFrontendLink()
    {
        var settings = Resolve(development: true);

        Assert.True(settings.Run);
        Assert.Equal("http://localhost:5173", settings.Options.PublicBaseUrl);
        Assert.Null(settings.Error);
    }

    [Fact]
    public void Production_WithSmtpAndValidUrl_Runs()
    {
        var settings = Resolve(development: false, url: Url, smtpHost: Smtp);

        Assert.True(settings.Run);
        Assert.Equal(Url, settings.Options.PublicBaseUrl);
        Assert.Null(settings.Error);
        Assert.Null(settings.Warning);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("zwrotnica.azurewebsites.net")]
    [InlineData("/sprawy")]
    [InlineData("ftp://zwrotnica.example")]
    public void Production_WithMissingOrInvalidUrl_NeverRuns(string? url)
    {
        var settings = Resolve(development: false, url: url, smtpHost: Smtp);

        Assert.False(settings.Run);
        Assert.Contains("PublicBaseUrl", settings.Error);
    }

    [Fact]
    public void Production_WithoutSmtp_IsOffByDefault_WithAWarning()
    {
        var settings = Resolve(development: false, url: Url);

        Assert.False(settings.Run);
        Assert.Null(settings.Error);
        Assert.Contains("Email__Smtp__Host", settings.Warning);
    }

    [Fact]
    public void Production_ExplicitlyEnabledWithoutSmtp_RunsToTheOutbox_WithAWarning()
    {
        var settings = Resolve(development: false, url: Url, enabled: "true");

        Assert.True(settings.Run);
        Assert.Contains(".eml", settings.Warning);
    }

    [Fact]
    public void ExplicitlyDisabled_NeverRuns()
    {
        Assert.False(Resolve(development: true, enabled: "false").Run);
        Assert.False(Resolve(development: false, url: Url, smtpHost: Smtp, enabled: "false").Run);
    }
}
