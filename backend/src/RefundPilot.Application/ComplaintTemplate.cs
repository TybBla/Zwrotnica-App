using System.Globalization;
using RefundPilot.Domain;

namespace RefundPilot.Application;

/// <summary>Case facts that go into a letter. Deliberately contains no personal data about the passenger.</summary>
public sealed record ComplaintFacts(
    string Carrier, string? TrainCategory, string From, string To, DateOnly TravelDate, string? TicketNumber,
    decimal TicketPrice, decimal Amount, IncidentType Incident, int DelayMinutes, DelayCause? Cause)
{
    public string AmountText => FormatMoney(Amount);
    public string PriceText => FormatMoney(TicketPrice);
    public string DateText => TravelDate.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);

    public string? CauseText => Cause switch
    {
        DelayCause.Weather => "warunki pogodowe",
        DelayCause.PersonsOnTrack => "osoby na torach",
        DelayCause.RailStrike => "strajk",
        DelayCause.Technical => "awaria techniczna",
        DelayCause.Other => "inna przyczyna",
        _ => null,
    };

    /// <summary>Polish money format, e.g. "10,00 zł" (no thousands separator: amounts here stay below 10 000).</summary>
    public static string FormatMoney(decimal value) =>
        value.ToString("F2", CultureInfo.InvariantCulture).Replace('.', ',') + " zł";
}

/// <summary>
/// Deterministic Polish complaint letter used when AI is off or its output fails validation.
/// Contains the tokens {{FULL_NAME}}, {{ADDRESS}} and {{PAYOUT}}, filled in by the service.
/// </summary>
public static class ComplaintTemplate
{
    public const string FullName = "{{FULL_NAME}}";
    public const string Address = "{{ADDRESS}}";
    public const string Iban = "{{IBAN}}";
    public const string Payout = "{{PAYOUT}}";

    public static (string Subject, string Body) Build(ComplaintFacts f)
    {
        var cancelled = f.Incident == IncidentType.Cancellation;
        var ticket = string.IsNullOrWhiteSpace(f.TicketNumber) ? "" : $" nr {f.TicketNumber}";
        var category = string.IsNullOrWhiteSpace(f.TrainCategory) ? "" : $" kat. {f.TrainCategory}";

        var subject = cancelled
            ? $"Wniosek o wypłatę odszkodowania – odwołanie pociągu {f.Carrier} z dnia {f.DateText}"
            : $"Wniosek o wypłatę odszkodowania – opóźnienie pociągu {f.Carrier} z dnia {f.DateText}";

        var what = cancelled
            ? "Pociąg został odwołany."
            : $"Pociąg dotarł do stacji docelowej z opóźnieniem {f.DelayMinutes} min.";
        if (f.CauseText is { } cause)
            what += $" Przyczyna zdarzenia (według dostępnych informacji): {cause}.";

        var body = $"""
            {FullName}
            {Address}

            {f.Carrier}

            Dotyczy: bilet{ticket}, przejazd relacją {f.From} – {f.To} w dniu {f.DateText}

            Szanowni Państwo,

            zwracam się z wnioskiem o wypłatę odszkodowania na podstawie art. 19 rozporządzenia (UE) 2021/782 Parlamentu Europejskiego i Rady z dnia 29 kwietnia 2021 r. w sprawie praw i obowiązków pasażerów kolejowych.

            W dniu {f.DateText} odbyłem(-am) podróż pociągiem{category} relacją {f.From} – {f.To} na podstawie biletu{ticket}, za który zapłaciłem(-am) {f.PriceText}. {what}

            Wnoszę o wypłatę odszkodowania w wysokości {f.AmountText}.

            {Payout}

            Proszę o rozpatrzenie wniosku i odpowiedź w terminie jednego miesiąca od dnia jego otrzymania.

            Z poważaniem
            {FullName}
            """;

        return (subject, body);
    }
}
