using System.Globalization;
using RefundPilot.Domain;

namespace RefundPilot.Application;

/// <summary>Case facts that go into a letter. Deliberately contains no personal data about the passenger.</summary>
public sealed record ComplaintFacts(
    string Carrier, string? TrainCategory, string? From, string? To, DateOnly TravelDate, string? TicketNumber,
    decimal TicketPrice, decimal Amount, IncidentType Incident, int DelayMinutes, DelayCause? Cause)
{
    /// <summary>Station name, or the token the service later turns into a placeholder when the station is unknown.</summary>
    public string FromText => string.IsNullOrWhiteSpace(From) ? ComplaintTemplate.FromStation : From;
    public string ToText => string.IsNullOrWhiteSpace(To) ? ComplaintTemplate.ToStation : To;
    public bool FromMissing => string.IsNullOrWhiteSpace(From);
    public bool ToMissing => string.IsNullOrWhiteSpace(To);

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
    public const string FromStation = "{{FROM_STATION}}";
    public const string ToStation = "{{TO_STATION}}";

    /// <summary>
    /// Legal basis in Reg. (EU) 2021/782: a cancelled train means reimbursement of the ticket (art. 18),
    /// a delay at the destination means compensation (art. 19).
    /// </summary>
    public static string Article(IncidentType incident) =>
        incident == IncidentType.Cancellation ? "art. 18" : "art. 19";

    public static (string Subject, string Body) Build(ComplaintFacts f)
    {
        var cancelled = f.Incident == IncidentType.Cancellation;
        var ticket = string.IsNullOrWhiteSpace(f.TicketNumber) ? "" : $" nr {f.TicketNumber}";
        var category = string.IsNullOrWhiteSpace(f.TrainCategory) ? "" : $" kat. {f.TrainCategory}";
        var claim = cancelled ? "zwrot należności za bilet" : "wypłatę odszkodowania";

        var subject = cancelled
            ? $"Wniosek o zwrot należności za bilet – odwołanie pociągu {f.Carrier} z dnia {f.DateText}"
            : $"Wniosek o wypłatę odszkodowania – opóźnienie pociągu {f.Carrier} z dnia {f.DateText}";

        // The art. 18 refund applies when the passenger gives up the journey, which is what this letter claims.
        var what = cancelled
            ? "Pociąg został odwołany i zrezygnowałem(-am) z podróży."
            : $"Pociąg dotarł do stacji docelowej z opóźnieniem {f.DelayMinutes} min.";
        if (f.CauseText is { } cause)
            what += $" Przyczyna zdarzenia (według dostępnych informacji): {cause}.";

        var body = $"""
            {FullName}
            {Address}

            {f.Carrier}

            Dotyczy: bilet{ticket}, przejazd relacją {f.FromText} – {f.ToText} w dniu {f.DateText}

            Szanowni Państwo,

            zwracam się z wnioskiem o {claim} na podstawie {Article(f.Incident)} rozporządzenia (UE) 2021/782 Parlamentu Europejskiego i Rady z dnia 29 kwietnia 2021 r. w sprawie praw i obowiązków pasażerów kolejowych.

            W dniu {f.DateText} odbyłem(-am) podróż pociągiem{category} relacją {f.FromText} – {f.ToText} na podstawie biletu{ticket}, za który zapłaciłem(-am) {f.PriceText}. {what}

            Wnoszę o {claim} w wysokości {f.AmountText}.

            {Payout}

            Proszę o rozpatrzenie wniosku i odpowiedź w terminie jednego miesiąca od dnia jego otrzymania.

            Z poważaniem
            {FullName}
            """;

        return (subject, body);
    }

    /// <summary>
    /// A complaint ("reklamacja") for a train outside the EU 25%/50% scheme (Polish regional, suburban and urban
    /// services are exempt) or one we could not classify: it states the facts and asks the carrier to decide under its
    /// own terms of carriage. No amount is demanded and no EU article is claimed as an entitlement.
    /// </summary>
    public static (string Subject, string Body) BuildCarrierRules(ComplaintFacts f)
    {
        var cancelled = f.Incident == IncidentType.Cancellation;
        var ticket = string.IsNullOrWhiteSpace(f.TicketNumber) ? "" : $" nr {f.TicketNumber}";
        var category = string.IsNullOrWhiteSpace(f.TrainCategory) ? "" : $" kat. {f.TrainCategory}";

        var subject = $"Reklamacja – {(cancelled ? "odwołanie" : "opóźnienie")} pociągu {f.Carrier} z dnia {f.DateText}";

        var what = cancelled
            ? "Pociąg został odwołany."
            : $"Pociąg dotarł do stacji docelowej z opóźnieniem {f.DelayMinutes} min.";
        if (f.CauseText is { } cause)
            what += $" Przyczyna zdarzenia (według dostępnych informacji): {cause}.";

        var body = $"""
            {FullName}
            {Address}

            {f.Carrier}

            Dotyczy: bilet{ticket}, przejazd relacją {f.FromText} – {f.ToText} w dniu {f.DateText}

            Szanowni Państwo,

            składam reklamację dotyczącą przejazdu w dniu {f.DateText} pociągiem{category} relacją {f.FromText} – {f.ToText}, odbytego na podstawie biletu{ticket}, za który zapłaciłem(-am) {f.PriceText}. {what}

            Proszę o rozpatrzenie reklamacji i przyznanie rekompensaty lub zwrotu należności za bilet na zasadach określonych w Państwa regulaminie przewozu.

            {Payout}

            Proszę o pisemną odpowiedź na reklamację.

            Z poważaniem
            {FullName}
            """;

        return (subject, body);
    }
}
