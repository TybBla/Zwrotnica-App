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
/// One e-mail per Sent case once the carrier's month has fully passed, to the owner's account address only.
/// Claim-then-send: due cases are claimed with one conditional UPDATE (ReminderSentAt), so two runs never send the same
/// reminder (at most once; a crash between claim and send loses that one e-mail). A permanent rejection keeps the
/// claim (handled, never retried); a temporary failure releases it for the next run; a dead mail server stops the run.
/// </summary>
public sealed class ReminderService(
    ICaseRepository cases, IEmailSender email, TimeProvider clock, ReminderOptions options, ILogger<ReminderService> logger)
    : IReminderService
{
    public async Task<int> SendDueAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        // The carrier has the whole last day (art. 112 KC): only deadlines that ended before today in Warsaw count.
        var dueBefore = WarsawClock.StartOfDayUtc(WarsawClock.DateOf(now));
        var due = await cases.ListDueRemindersAsync(dueBefore, ct);
        if (due.Count == 0)
            return 0;

        var claimed = (await cases.ClaimRemindersAsync(due.Select(d => d.Case.Id).ToList(), now, ct)).ToHashSet();
        var handled = new HashSet<int>();
        var sent = 0;

        try
        {
            foreach (var (refundCase, to) in due.Where(d => claimed.Contains(d.Case.Id)))
            {
                ct.ThrowIfCancellationRequested();
                var message = ReminderEmail.Build(refundCase, options.PublicBaseUrl);
                try
                {
                    await email.SendAsync(to, message.Subject, message.Text, message.Html, ct);
                    handled.Add(refundCase.Id);
                    sent++;
                }
                catch (EmailRecipientRejectedException ex)
                {
                    handled.Add(refundCase.Id);
                    logger.Log(ex.Reason == EmailRejection.ReservedDomain ? LogLevel.Information : LogLevel.Warning,
                        "Reminder for case {CaseId} will not be sent ({Reason}).", refundCase.Id, ex.Reason);
                }
                catch (EmailTransportException ex) when (!ct.IsCancellationRequested)
                {
                    logger.LogWarning("Mail server unavailable ({Error}); reminders paused until the next run.",
                        ex.InnerException?.GetType().Name ?? ex.GetType().Name);
                    break;
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
            // Everything claimed but not handled (failed, skipped after a dead server, or cancelled) goes back.
            var release = claimed.Where(id => !handled.Contains(id)).ToList();
            if (release.Count > 0)
                await cases.ReleaseRemindersAsync(release, now, CancellationToken.None);
        }

        if (sent > 0)
            logger.LogInformation("Sent {Count} reminder e-mail(s).", sent);
        return sent;
    }
}
