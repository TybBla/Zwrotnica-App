using RefundPilot.Application.Services;

namespace RefundPilot.Api;

/// <summary>
/// Whether the reminder job runs, decided once at startup. Reminders:Enabled wins when set; otherwise they run in
/// Development (local .eml outbox) and elsewhere only with an SMTP host. Outside Development a missing or
/// non-absolute http(s) PublicBaseUrl stops them: an e-mail must never link to localhost.
/// </summary>
public sealed record ReminderSettings(bool Run, ReminderOptions Options, TimeSpan Interval, string? Error, string? Warning)
{
    public const string DevelopmentBaseUrl = "http://localhost:5173";

    public static ReminderSettings From(IConfiguration configuration, bool isDevelopment)
    {
        var hasSmtp = !string.IsNullOrWhiteSpace(configuration["Email:Smtp:Host"]);
        var explicitlyEnabled = bool.TryParse(configuration["Reminders:Enabled"], out var flag) ? flag : (bool?)null;
        var enabled = explicitlyEnabled ?? (isDevelopment || hasSmtp);
        var minutes = configuration.GetValue("Reminders:IntervalMinutes", defaultValue: 60);
        var interval = TimeSpan.FromMinutes(minutes > 0 ? minutes : 60);

        var configuredUrl = configuration["PublicBaseUrl"]?.Trim();
        var validUrl = Uri.TryCreate(configuredUrl, UriKind.Absolute, out var uri)
                       && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
        var options = new ReminderOptions(validUrl ? configuredUrl! : DevelopmentBaseUrl);

        if (isDevelopment)
            return new(enabled, options, interval, null, null);

        string? warning = null;
        if (!hasSmtp)
            warning = enabled
                ? "No SMTP host (Email__Smtp__Host): reminder e-mails are only written as .eml files to the outbox folder."
                : "Reminder e-mails are off: set Email__Smtp__Host (and PublicBaseUrl) to send them.";

        if (enabled && !validUrl)
            return new(false, options, interval,
                "PublicBaseUrl is missing or not an absolute http(s) URL: reminder e-mails are off.", warning);

        return new(enabled, options, interval, null, warning);
    }
}
