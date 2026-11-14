using System.Globalization;
using RefundPilot.Application.Dtos;
using RefundPilot.Domain;

namespace RefundPilot.Application;

/// <summary>
/// "Werdykt końcowy": the legal effect a complaint can be expected to have, stated formally. Built only from the
/// deterministic rules (eligibility, amount, refusal risk) – never by the AI. Non-promissory wherever the outcome
/// depends on the carrier.
/// </summary>
public static class FinalVerdict
{
    private const string Regulation = "rozporządzenia (UE) 2021/782";

    private const string ForceMajeure =
        "Przewoźnik może odmówić wypłaty, jeżeli wykaże, że opóźnienie wynikło z okoliczności nadzwyczajnych (siły wyższej).";

    /// <summary>A train covered by the EU scheme with a known price: compensation, a refund, or nothing due.</summary>
    public static OutcomeDto Covered(
        IncidentType incident, CompensationResult compensation, bool refusalRisk, decimal minimumPayout)
    {
        var amount = ComplaintFacts.FormatMoney(compensation.Amount);

        // The art. 18 refund for a cancellation is due whatever caused it, so no force-majeure caveat there.
        if (incident == IncidentType.Cancellation)
            return new(
                "Przewoźnik jest zobowiązany do zwrotu ceny biletu",
                $"Na podstawie art. 18 {Regulation} przewoźnik jest zobowiązany zwrócić pełną cenę biletu za odwołany pociąg, tj. {amount}.",
                $"Art. 18 {Regulation}",
                "Przewoźnik powinien zwrócić należność w terminie 1 miesiąca od złożenia wniosku.");

        if (compensation.Percent == 0)
            return new(
                "Przewoźnik nie ma obowiązku wypłaty odszkodowania",
                $"Opóźnienie na stacji docelowej nie przekroczyło 60 minut, dlatego przepisy {Regulation} nie przewidują " +
                "odszkodowania. Możesz złożyć reklamację – przewoźnik rozpatrzy ją według swojego regulaminu.",
                $"Art. 19 {Regulation}",
                null);

        var statement =
            $"Na podstawie art. 19 {Regulation} przewoźnik jest zobowiązany wypłacić odszkodowanie w wysokości " +
            $"{compensation.Percent}% ceny biletu, tj. {amount}.";
        if (compensation.BelowMinimum)
            statement += $" Przewoźnik może nie wypłacić odszkodowania niższego niż {minimumPayout.ToString("0.##", CultureInfo.InvariantCulture)} zł " +
                         "– rozporządzenie pozwala ustalić taki próg minimalny w regulaminie przewoźnika.";
        if (refusalRisk)
            statement += " " + ForceMajeure;

        return new(
            "Przewoźnik jest zobowiązany do wypłaty odszkodowania",
            statement,
            $"Art. 19 {Regulation}",
            "Przewoźnik powinien wypłacić odszkodowanie w terminie 1 miesiąca od złożenia wniosku.");
    }

    /// <summary>A Polish regional, suburban or urban service: exempt from the EU compensation scheme.</summary>
    public static OutcomeDto NotCovered() => new(
        "Przewoźnik nie ma ustawowego obowiązku wypłaty odszkodowania",
        $"Przewozy regionalne, podmiejskie i miejskie w Polsce są wyłączone z systemu odszkodowań określonego w {Regulation}. " +
        "Przewoźnik może jednak przyznać rekompensatę na zasadach określonych w swoim regulaminie przewozu.",
        null,
        null);

    /// <summary>Season ticket, InterRegio without Polregio, or an unrecognised carrier: say why it is unclear.</summary>
    public static OutcomeDto Unclear(string reason) => new(
        "Nie można jednoznacznie ustalić uprawnień",
        $"{reason} Zalecamy sprawdzenie regulaminu przewoźnika; reklamację można złożyć niezależnie od wyniku.",
        null,
        null);
}
