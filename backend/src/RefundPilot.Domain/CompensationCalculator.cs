namespace RefundPilot.Domain;

public sealed record CompensationOptions(decimal MinimumPayout = 16m);

public sealed record CompensationResult(
    decimal Amount, int Percent, bool BelowMinimum, bool PriceRequired, string Reason);

public sealed class CompensationCalculator(CompensationOptions options)
{
    // Art. 19 Reg. (EU) 2021/782: delay at destination of 60-119 min = 25%, >=120 min = 50%
    // of the price actually paid; cancellation = 100% refund of the ticket (art. 18).
    public CompensationResult Calculate(IncidentType type, int delayMinutes, decimal ticketPrice)
    {
        if (ticketPrice <= 0)
            return new(0m, 0, false, true, "Podaj cenę zapłaconego biletu, aby obliczyć kwotę.");

        var percent = type == IncidentType.Cancellation ? 100 : PercentForDelay(delayMinutes);
        if (percent == 0)
            return new(0m, 0, false, false, "Opóźnienie poniżej 60 minut nie daje prawa do rekompensaty.");

        var amount = Math.Round(ticketPrice * percent / 100m, 2, MidpointRounding.AwayFromZero);
        // The carrier minimum payout applies to art. 19 delay compensation only, never to the art. 18 refund.
        var belowMinimum = type != IncidentType.Cancellation && amount < options.MinimumPayout;
        var reason = belowMinimum
            ? $"{percent}% ceny biletu, ale kwota jest poniżej minimalnej wypłaty ({options.MinimumPayout:0.##} zł) – przewoźnik może jej nie wypłacić."
            : type == IncidentType.Cancellation
                ? "Zwrot pełnej ceny zapłaconego biletu za odwołany pociąg."
                : $"{percent}% ceny zapłaconego biletu.";

        return new(amount, percent, belowMinimum, false, reason);
    }

    private static int PercentForDelay(int delayMinutes) => delayMinutes switch
    {
        >= 120 => 50,
        >= 60 => 25,
        _ => 0,
    };
}
