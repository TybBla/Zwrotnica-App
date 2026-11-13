namespace RefundPilot.Application;

/// <summary>"Today" for a Polish passenger is the Europe/Warsaw calendar date, not the UTC one.</summary>
public static class WarsawClock
{
    private static readonly TimeZoneInfo Zone = Resolve();

    public static DateOnly Today(TimeProvider clock) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), Zone).DateTime);

    private static TimeZoneInfo Resolve()
    {
        foreach (var id in new[] { "Europe/Warsaw", "Central European Standard Time" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }
        return TimeZoneInfo.Utc;
    }
}
