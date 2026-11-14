namespace RefundPilot.Application;

/// <summary>"Today" for a Polish passenger is the Europe/Warsaw calendar date, not the UTC one.</summary>
public static class WarsawClock
{
    private static readonly TimeZoneInfo Zone = Resolve();

    public static DateOnly Today(TimeProvider clock) => DateOf(clock.GetUtcNow().UtcDateTime);

    /// <summary>The Warsaw calendar date of a UTC instant.</summary>
    public static DateOnly DateOf(DateTime utc) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Zone));

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
