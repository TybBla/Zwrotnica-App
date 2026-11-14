using RefundPilot.Application.Services;

namespace RefundPilot.Api;

/// <summary>
/// Sends due reminder e-mails at startup and then every Reminders:IntervalMinutes (default 60), each run in its own
/// DI scope (fresh DbContext). It only runs while the process is up: on a plan that sleeps when idle (Azure F1) the
/// e-mails go out once the app wakes; a production setup would trigger ReminderService from a scheduled job
/// (e.g. an Azure Functions timer or a WebJob) instead.
/// </summary>
public sealed class ReminderBackgroundService(
    IServiceScopeFactory scopes, TimeSpan interval, ILogger<ReminderBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();
        using var timer = new PeriodicTimer(interval);
        try
        {
            do
            {
                await RunOnceAsync(stoppingToken);
            } while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<IReminderService>().SendDueAsync(ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            // Never take the host down: the next tick tries again.
            logger.LogError(ex, "Reminder run failed.");
        }
    }
}
