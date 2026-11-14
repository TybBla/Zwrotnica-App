namespace RefundPilot.Domain;

public sealed record EligibilityResult(Verdict Verdict, string Reason);

public sealed class EligibilityService
{
    public EligibilityResult Assess(string? carrier, string? trainCategory)
    {
        var carrierText = CarrierCatalog.Normalize(carrier);
        var categoryText = CarrierCatalog.Normalize(trainCategory);

        if (CarrierCatalog.IsRegional(carrierText))
            return new(Verdict.NotCovered,
                "Pociągi regionalne, miejskie i podmiejskie są w Polsce wyłączone z rekompensat 25%/50%.");

        if (CarrierCatalog.HasInternationalMarker(carrierText) || CarrierCatalog.HasInternationalMarker(categoryText))
            return new(Verdict.Eligible, "Połączenie międzynarodowe objęte rozporządzeniem (UE) 2021/782.");

        if (CarrierCatalog.IsInterRegio(categoryText))
            return new(Verdict.Unknown,
                "Pociągi InterRegio prowadzi Polregio. Należy sprawdzić w regulaminie przewoźnika, czy przysługuje odszkodowanie.");

        if (CarrierCatalog.IsPkpIntercity(carrierText) && CarrierCatalog.HasIntercityCategory(categoryText))
            return new(Verdict.Eligible, "Pociąg PKP Intercity objęty rekompensatą 25%/50% ceny biletu.");

        return new(Verdict.Unknown, "Nie udało się jednoznacznie określić przewoźnika lub kategorii pociągu.");
    }
}
