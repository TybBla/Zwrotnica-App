using RefundPilot.Application.Dtos;
using RefundPilot.Application.Services;
using RefundPilot.Domain;

namespace RefundPilot.Tests.Application;

/// <summary>"Werdykt końcowy": the expected legal effect of the complaint, derived only from the deterministic rules.</summary>
public sealed class AssessmentOutcomeTests
{
    private readonly AssessmentService _service =
        new(new EligibilityService(), new CompensationCalculator(new CompensationOptions()), new CompensationOptions());

    private OutcomeDto? Outcome(string carrier = "PKP Intercity", string? category = "IC", int delay = 75,
        decimal price = 100m, IncidentType type = IncidentType.Delay, DelayCause? cause = null, bool season = false) =>
        _service.Assess(new AssessmentRequest(carrier, category, type, delay, price, cause, season)).Outcome;

    [Theory]
    [InlineData(75, 25, "25,00 zł")]
    [InlineData(130, 50, "50,00 zł")]
    public void CoveredDelay_CarrierMustPayCompensationUnderArticle19(int delay, int percent, string amount)
    {
        var outcome = Outcome(delay: delay)!;

        Assert.Equal("Przewoźnik jest zobowiązany do wypłaty odszkodowania", outcome.Title);
        Assert.Equal(
            $"Na podstawie art. 19 rozporządzenia (UE) 2021/782 przewoźnik jest zobowiązany wypłacić odszkodowanie w wysokości {percent}% ceny biletu, tj. {amount}.",
            outcome.Statement);
        Assert.Equal("Art. 19 rozporządzenia (UE) 2021/782", outcome.LegalBasis);
        Assert.Equal("Przewoźnik powinien wypłacić odszkodowanie w terminie 1 miesiąca od złożenia wniosku.", outcome.Deadline);
    }

    [Fact]
    public void CoveredCancellation_CarrierMustRefundTheTicketUnderArticle18_EvenInBadWeather()
    {
        var outcome = Outcome(type: IncidentType.Cancellation, delay: 0, price: 96.5m, cause: DelayCause.Weather)!;

        Assert.Equal("Przewoźnik jest zobowiązany do zwrotu ceny biletu", outcome.Title);
        Assert.Contains("art. 18 rozporządzenia (UE) 2021/782", outcome.Statement);
        Assert.Contains("pełną cenę biletu", outcome.Statement);
        Assert.Contains("96,50 zł", outcome.Statement);
        Assert.DoesNotContain("okoliczności nadzwyczajnych", outcome.Statement);
        Assert.Equal("Art. 18 rozporządzenia (UE) 2021/782", outcome.LegalBasis);
        Assert.Contains("1 miesiąca", outcome.Deadline);
    }

    [Theory]
    [InlineData(DelayCause.Weather)]
    [InlineData(DelayCause.PersonsOnTrack)]
    public void RefusalRisk_AddsTheExtraordinaryCircumstancesCaveat(DelayCause cause)
    {
        var outcome = Outcome(cause: cause)!;

        Assert.EndsWith(
            "Przewoźnik może odmówić wypłaty, jeżeli wykaże, że opóźnienie wynikło z okoliczności nadzwyczajnych (siły wyższej).",
            outcome.Statement);
    }

    [Fact]
    public void BelowMinimum_SaysTheCarrierMayNotPaySoSmallAnAmount()
    {
        var outcome = Outcome(price: 40m)!;

        Assert.Contains("tj. 10,00 zł.", outcome.Statement);
        Assert.Contains("Przewoźnik może nie wypłacić odszkodowania niższego niż 16 zł", outcome.Statement);
        Assert.Equal("Przewoźnik jest zobowiązany do wypłaty odszkodowania", outcome.Title);
    }

    [Fact]
    public void CoveredDelayUnder60Minutes_NoObligationButComplaintPossible()
    {
        var outcome = Outcome(delay: 45)!;

        Assert.Equal("Przewoźnik nie ma obowiązku wypłaty odszkodowania", outcome.Title);
        Assert.Equal(
            "Opóźnienie na stacji docelowej nie przekroczyło 60 minut, dlatego przepisy rozporządzenia (UE) 2021/782 " +
            "nie przewidują odszkodowania. Możesz złożyć reklamację – przewoźnik rozpatrzy ją według swojego regulaminu.",
            outcome.Statement);
        Assert.Null(outcome.Deadline);
    }

    [Fact]
    public void RegionalCarrier_NoStatutoryObligation()
    {
        var outcome = Outcome(carrier: "Polregio", category: null, delay: 200)!;

        Assert.Equal("Przewoźnik nie ma ustawowego obowiązku wypłaty odszkodowania", outcome.Title);
        Assert.StartsWith("Przewozy regionalne, podmiejskie i miejskie w Polsce są wyłączone z systemu odszkodowań", outcome.Statement);
        Assert.EndsWith("na zasadach określonych w swoim regulaminie przewozu.", outcome.Statement);
        Assert.Null(outcome.LegalBasis);
        Assert.Null(outcome.Deadline);
    }

    [Theory]
    [InlineData("PKP Intercity", "IC", true)]       // season ticket
    [InlineData("Leo Express", "IR", false)]        // InterRegio with another carrier
    [InlineData("Nieznany Przewoźnik", null, false)] // unknown carrier
    public void Unclear_SaysEntitlementCannotBeDeterminedAndWhy(string carrier, string? category, bool season)
    {
        var assessment = _service.Assess(new AssessmentRequest(carrier, category, IncidentType.Delay, 90, 100m, null, season));
        var outcome = assessment.Outcome!;

        Assert.Equal(Verdict.Unknown, assessment.Verdict);
        Assert.Equal("Nie można jednoznacznie ustalić uprawnień", outcome.Title);
        Assert.StartsWith(assessment.Reason, outcome.Statement);
        Assert.EndsWith("Zalecamy sprawdzenie regulaminu przewoźnika; reklamację można złożyć niezależnie od wyniku.", outcome.Statement);
    }

    [Fact]
    public void MissingPrice_HasNoOutcome()
    {
        Assert.Null(Outcome(price: 0m));
    }
}
