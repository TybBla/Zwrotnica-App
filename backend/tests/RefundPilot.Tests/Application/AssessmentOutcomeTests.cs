using RefundPilot.Application.Dtos;
using RefundPilot.Application.Services;
using RefundPilot.Domain;

namespace RefundPilot.Tests.Application;

/// <summary>"Werdykt końcowy": the expected legal effect of the complaint, derived only from the deterministic rules.</summary>
public sealed class AssessmentOutcomeTests
{
    private const string Art19Basis = "Art. 19 rozporządzenia (UE) 2021/782";
    private const string CompensationDeadline = "Przewoźnik powinien wypłacić odszkodowanie w terminie 1 miesiąca od złożenia wniosku.";
    private const string ForceMajeure =
        "Przewoźnik może odmówić wypłaty, jeżeli wykaże, że opóźnienie wynikło z okoliczności nadzwyczajnych (siły wyższej).";
    private const string BelowMinimumSentence =
        "Przewoźnik może nie wypłacić odszkodowania niższego niż 16 zł – rozporządzenie pozwala ustalić taki próg minimalny w regulaminie przewoźnika.";

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

        Assert.Equal(new OutcomeDto(
            "Przewoźnik jest zobowiązany do wypłaty odszkodowania",
            $"Na podstawie art. 19 rozporządzenia (UE) 2021/782 przewoźnik jest zobowiązany wypłacić odszkodowanie w wysokości {percent}% ceny biletu, tj. {amount}.",
            Art19Basis,
            CompensationDeadline), outcome);
    }

    [Fact]
    public void CoveredCancellation_RefundWhenThePassengerGivesUpTheJourney_EvenInBadWeather()
    {
        var outcome = Outcome(type: IncidentType.Cancellation, delay: 0, price: 96.5m, cause: DelayCause.Weather)!;

        Assert.Equal(new OutcomeDto(
            "Przysługuje zwrot ceny biletu",
            "Na podstawie art. 18 ust. 1 lit. a rozporządzenia (UE) 2021/782, w przypadku odwołania pociągu i rezygnacji z podróży " +
            "przewoźnik jest zobowiązany zwrócić pełną cenę biletu, tj. 96,50 zł. Jeżeli podróż odbyła się innym pociągiem, " +
            "zamiast zwrotu może przysługiwać odszkodowanie za opóźnienie.",
            "Art. 18 ust. 1 lit. a w zw. z art. 19 rozporządzenia (UE) 2021/782",
            "Zwrot powinien nastąpić na tych samych warunkach co wypłata odszkodowania, tj. w terminie 1 miesiąca od złożenia wniosku."),
            outcome);
    }

    [Theory]
    [InlineData(DelayCause.Weather)]
    [InlineData(DelayCause.PersonsOnTrack)]
    public void RefusalRisk_AddsTheExtraordinaryCircumstancesCaveat(DelayCause cause)
    {
        var outcome = Outcome(cause: cause)!;

        Assert.Equal(
            "Na podstawie art. 19 rozporządzenia (UE) 2021/782 przewoźnik jest zobowiązany wypłacić odszkodowanie w wysokości 25% ceny biletu, tj. 25,00 zł. " +
            ForceMajeure,
            outcome.Statement);
    }

    [Fact]
    public void BelowMinimum_DoesNotPromisePaymentAndHasNoDeadline()
    {
        var outcome = Outcome(price: 40m)!;

        Assert.Equal(new OutcomeDto(
            "Odszkodowanie przysługuje co do zasady, ale kwota jest poniżej progu wypłaty",
            "Na podstawie art. 19 rozporządzenia (UE) 2021/782 odszkodowanie wynosi 25% ceny biletu, tj. 10,00 zł. " + BelowMinimumSentence,
            Art19Basis,
            null), outcome);
    }

    [Fact]
    public void BelowMinimumWithRefusalRisk_HasBothCaveats()
    {
        var outcome = Outcome(price: 40m, cause: DelayCause.Weather)!;

        Assert.Equal(
            "Na podstawie art. 19 rozporządzenia (UE) 2021/782 odszkodowanie wynosi 25% ceny biletu, tj. 10,00 zł. " +
            BelowMinimumSentence + " " + ForceMajeure,
            outcome.Statement);
    }

    [Fact]
    public void CoveredDelayUnder60Minutes_NoObligationButComplaintPossible()
    {
        var outcome = Outcome(delay: 45)!;

        Assert.Equal(new OutcomeDto(
            "Przewoźnik nie ma obowiązku wypłaty odszkodowania",
            "Opóźnienie na stacji docelowej wyniosło mniej niż 60 minut, dlatego przepisy rozporządzenia (UE) 2021/782 " +
            "nie przewidują odszkodowania. Reklamację można jednak złożyć – przewoźnik rozpatrzy ją według swojego regulaminu.",
            Art19Basis,
            null), outcome);
    }

    [Fact]
    public void RegionalCarrier_NoAutomaticCompensation_OnlyProvenDamage()
    {
        var outcome = Outcome(carrier: "Polregio", category: null, delay: 200)!;

        Assert.Equal(new OutcomeDto(
            "Przewoźnik nie ma obowiązku wypłaty automatycznego odszkodowania",
            RegionalStatement,
            null,
            null), outcome);
    }

    private const string RegionalStatement =
        "Przewozy regionalne, podmiejskie i miejskie w Polsce są wyłączone z systemu automatycznych odszkodowań " +
        "(25% lub 50% ceny biletu) określonego w rozporządzeniu (UE) 2021/782. Tacy przewoźnicy zwykle uwzględniają " +
        "jedynie roszczenia o zwrot udowodnionej, udokumentowanej szkody, np. kosztów taksówki lub noclegu.";

    [Fact]
    public void RegionalCarrierWithExpense_StatesTheClaimedCostsWithoutADeadline()
    {
        var outcome = _service.Assess(new AssessmentRequest(
            "Koleje Mazowieckie", null, IncidentType.Cancellation, 0, 18m, null, false, ExpenseAmount: 85m)).Outcome!;

        Assert.Equal(new OutcomeDto(
            "Przewoźnik nie ma obowiązku wypłaty automatycznego odszkodowania",
            RegionalStatement + " Wniosek obejmuje zwrot udokumentowanych kosztów w kwocie 85,00 zł.",
            null,
            null), outcome);
    }

    [Fact]
    public void ExpenseAmount_IsIgnoredOutsideRegionalCarriers()
    {
        var outcome = _service.Assess(new AssessmentRequest(
            "PKP Intercity", "IC", IncidentType.Delay, 75, 100m, null, false, ExpenseAmount: 85m)).Outcome!;

        Assert.DoesNotContain("kosztów", outcome.Statement);
    }

    [Theory]
    [InlineData("PKP Intercity", "IC", true,
        "Bilety okresowe wymagają osobnego wyliczenia – nie podajemy kwoty automatycznie. Należy sprawdzić regulamin przewoźnika. " +
        "Reklamację można złożyć niezależnie od wyniku.")]
    [InlineData("Leo Express", "IR", false,
        "Pociągi InterRegio prowadzi Polregio. Należy sprawdzić w regulaminie przewoźnika, czy przysługuje odszkodowanie. " +
        "Reklamację można złożyć niezależnie od wyniku.")]
    [InlineData("Nieznany Przewoźnik", null, false,
        "Nie udało się jednoznacznie określić przewoźnika lub kategorii pociągu. Należy sprawdzić regulamin przewoźnika. " +
        "Reklamację można złożyć niezależnie od wyniku.")]
    public void Unclear_SaysWhyOnceAndThatAComplaintIsPossible(string carrier, string? category, bool season, string statement)
    {
        var assessment = _service.Assess(new AssessmentRequest(carrier, category, IncidentType.Delay, 90, 100m, null, season));

        Assert.Equal(Verdict.Unknown, assessment.Verdict);
        Assert.Equal(new OutcomeDto("Nie można jednoznacznie ustalić uprawnień", statement, null, null), assessment.Outcome);
    }

    [Fact]
    public void MissingPrice_HasNoOutcome()
    {
        Assert.Null(Outcome(price: 0m));
    }
}
