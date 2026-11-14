using Microsoft.Extensions.Logging;
using RefundPilot.Application.Abstractions;

namespace RefundPilot.Application.Services;

/// <summary>PublicBaseUrl: where the SPA is served, for the case link in the e-mail (e.g. http://localhost:5173).</summary>
public sealed record ReminderOptions(string PublicBaseUrl);

public interface IReminderService
{
    /// <summary>Sends the follow-up e-mail for every due case and returns how many went out.</summary>
    Task<int> SendDueAsync(CancellationToken ct);
}

/// <summary>
/// One e-mail per Sent case once the carrier's month has passed, to the owner's account address only.
/// Due cases come from one query and are marked in one statement; a failed send is logged and retried on the next run.
/// </summary>
public sealed class ReminderService(
    ICaseRepository cases, IEmailSender email, TimeProvider clock, ReminderOptions options, ILogger<ReminderService> logger)
    : IReminderService
{
    public async Task<int> SendDueAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var due = await cases.ListDueRemindersAsync(now, ct);
        var sent = new List<int>(due.Count);

        try
        {
            foreach (var (refundCase, to) in due)
            {
                ct.ThrowIfCancellationRequested();
                var message = ReminderEmail.Build(refundCase, options.PublicBaseUrl);
                try
                {
                    await email.SendAsync(to, message.Subject, message.Text, message.Html, ct);
                    sent.Add(refundCase.Id);
                }
                catch (Exception ex) when (!ct.IsCancellationRequested)
                {
                    // Only the type: provider messages can echo the recipient's address.
                    logger.LogWarning("Reminder for case {CaseId} was not sent ({Error}); retrying on the next run.",
                        refundCase.Id, ex.GetType().Name);
                }
            }
        }
        finally
        {
            // Also when stopping mid-run: what already went out must not go out twice.
            if (sent.Count > 0)
                await cases.MarkRemindersSentAsync(sent, now, CancellationToken.None);
        }

        if (sent.Count > 0)
            logger.LogInformation("Sent {Count} reminder e-mail(s).", sent.Count);
        return sent.Count;
    }
}
