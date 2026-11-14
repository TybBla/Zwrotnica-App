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
    // Genitive ("na podstawie art. 19 rozporządzenia …") and locative ("określonego w rozporządzeniu …").
    private const string Regulation = "rozporządzenia (UE) 2021/782";
    private const string InRegulation = "w rozporządzeniu (UE) 2021/782";

    private const string ForceMajeure =
        "Przewoźnik może odmówić wypłaty, jeżeli wykaże, że opóźnienie wynikło z okoliczności nadzwyczajnych (siły wyższej).";

    private const string ComplaintPossible = "Reklamację można złożyć niezależnie od wyniku.";

    /// <summary>A train covered by the EU scheme with a known price: compensation, a refund, or nothing due.</summary>
    public static OutcomeDto Covered(
        IncidentType incident, CompensationResult compensation, bool refusalRisk, decimal minimumPayout)
    {
        var amount = ComplaintFacts.FormatMoney(compensation.Amount);

        // Art. 18 lets the passenger choose: the full refund applies when the journey is given up. It does not depend
        // on the cause, so there is no force-majeure caveat here.
        if (incident == IncidentType.Cancellation)
            return new(
                "Przysługuje zwrot ceny biletu",
                $"Na podstawie art. 18 ust. 1 lit. a {Regulation}, w przypadku odwołania pociągu i rezygnacji z podróży " +
                $"przewoźnik jest zobowiązany zwrócić pełną cenę biletu, tj. {amount}. Jeżeli podróż odbyła się innym " +
                "pociągiem, zamiast zwrotu może przysługiwać odszkodowanie za opóźnienie.",
                $"Art. 18 ust. 1 lit. a w zw. z art. 19 {Regulation}",
                "Zwrot powinien nastąpić na tych samych warunkach co wypłata odszkodowania, tj. w terminie 1 miesiąca od złożenia wniosku.");

        if (compensation.Percent == 0)
            return new(
                "Przewoźnik nie ma obowiązku wypłaty odszkodowania",
                $"Opóźnienie na stacji docelowej wyniosło mniej niż 60 minut, dlatego przepisy {Regulation} nie przewidują " +
                "odszkodowania. Reklamację można jednak złożyć – przewoźnik rozpatrzy ją według swojego regulaminu.",
                $"Art. 19 {Regulation}",
                null);

        // Below the carrier's minimum the payout is not certain: no promise in the title and no payment deadline.
        if (compensation.BelowMinimum)
        {
            var minimum = minimumPayout.ToString("0.##", CultureInfo.InvariantCulture);
            var belowStatement =
                $"Na podstawie art. 19 {Regulation} odszkodowanie wynosi {compensation.Percent}% ceny biletu, tj. {amount}. " +
                $"Przewoźnik może nie wypłacić odszkodowania niższego niż {minimum} zł – rozporządzenie pozwala ustalić taki " +
                "próg minimalny w regulaminie przewoźnika.";
            return new(
                "Odszkodowanie przysługuje co do zasady, ale kwota jest poniżej progu wypłaty",
                refusalRisk ? $"{belowStatement} {ForceMajeure}" : belowStatement,
                $"Art. 19 {Regulation}",
                null);
        }

        var statement =
            $"Na podstawie art. 19 {Regulation} przewoźnik jest zobowiązany wypłacić odszkodowanie w wysokości " +
            $"{compensation.Percent}% ceny biletu, tj. {amount}.";
        return new(
            "Przewoźnik jest zobowiązany do wypłaty odszkodowania",
            refusalRisk ? $"{statement} {ForceMajeure}" : statement,
            $"Art. 19 {Regulation}",
            "Przewoźnik powinien wypłacić odszkodowanie w terminie 1 miesiąca od złożenia wniosku.");
    }

    /// <summary>
    /// A Polish regional, suburban or urban service: exempt from the EU 25%/50% scheme; such carriers usually accept only
    /// claims for proven, documented damage. The costs the passenger entered are stated, but nothing is promised.
    /// </summary>
    public static OutcomeDto NotCovered(decimal? expenseAmount = null)
    {
        var statement =
            $"Przewozy regionalne, podmiejskie i miejskie w Polsce są wyłączone z systemu automatycznych odszkodowań " +
            $"(25% lub 50% ceny biletu) określonego {InRegulation}. Tacy przewoźnicy zwykle uwzględniają jedynie " +
            "roszczenia o zwrot udowodnionej, udokumentowanej szkody, np. kosztów taksówki lub noclegu.";
        if (expenseAmount is > 0 and var amount)
            statement += $" Wniosek obejmuje zwrot udokumentowanych kosztów w kwocie {ComplaintFacts.FormatMoney(amount)}.";
        return new("Przewoźnik nie ma obowiązku wypłaty automatycznego odszkodowania", statement, null, null);
    }

    /// <summary>
    /// Season ticket, InterRegio without Polregio, or an unrecognised carrier: say why it is unclear. The advice to
    /// check the carrier's rules is added only when the reason does not already give it.
    /// </summary>
    public static OutcomeDto Unclear(string reason)
    {
        var check = reason.Contains("regulamin", StringComparison.OrdinalIgnoreCase)
            ? ""
            : " Należy sprawdzić regulamin przewoźnika.";
        return new("Nie można jednoznacznie ustalić uprawnień", $"{reason}{check} {ComplaintPossible}", null, null);
    }
}
