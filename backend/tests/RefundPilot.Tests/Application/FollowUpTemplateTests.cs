using RefundPilot.Application;
using RefundPilot.Domain;

namespace RefundPilot.Tests.Application;

public sealed class FollowUpTemplateTests
{
    private static readonly DateOnly Filed = new(2026, 10, 2);

    private static ComplaintFacts Facts(IncidentType incident = IncidentType.Delay, decimal amount = 10m,
        decimal? expense = null, string carrier = "PKP Intercity") =>
        new(carrier, carrier == "PKP Intercity" ? "IC" : null, "Gdynia Główna", "Kraków Główny", new DateOnly(2026, 10, 1),
            "IC-4821", 40m, amount, incident, incident == IncidentType.Delay ? 70 : 0, null,
            expense is null ? null : "taksówka", expense);

    private static FollowUpFacts FollowUp(ComplaintFacts facts, LetterKind kind, DateOnly? filed = null,
        bool pastDeadline = true, bool rejected = false) =>
        new(facts, kind, filed ?? Filed, pastDeadline, rejected);

    [Fact]
    public void Reminder_Compensation_RefersToTheClaimAmountAndArticle19()
    {
        var (subject, body) = FollowUpTemplate.BuildReminder(FollowUp(Facts(), LetterKind.Compensation));

        Assert.StartsWith("Ponaglenie", subject);
        Assert.Contains("02.10.2026", subject);
        Assert.Contains("PKP Intercity", body);
        Assert.Contains("w dniu 02.10.2026", body);
        Assert.Contains("bilet nr IC-4821", body);
        Assert.Contains("Gdynia Główna – Kraków Główny", body);
        Assert.Contains("wniosek o wypłatę odszkodowania", body);
        Assert.Contains("art. 19 rozporządzenia (UE) 2021/782", body);
        Assert.Contains("10,00 zł", body);
        Assert.Contains("do dnia dzisiejszego nie otrzymałem(-am) odpowiedzi", body);
        Assert.Contains("upłynął", body);
        Assert.Contains("w terminie 14 dni", body);
        Assert.Contains(ComplaintTemplate.FullName, body);
        Assert.Contains(ComplaintTemplate.Address, body);
        Assert.Contains(ComplaintTemplate.Payout, body);
    }

    [Fact]
    public void Reminder_BeforeTheDeadline_DoesNotClaimItHasPassed()
    {
        var (_, body) = FollowUpTemplate.BuildReminder(FollowUp(Facts(), LetterKind.Compensation, pastDeadline: false));

        Assert.DoesNotContain("upłynął", body);
        Assert.Contains("do dnia dzisiejszego nie otrzymałem(-am) odpowiedzi", body);
    }

    [Fact]
    public void Reminder_Cancellation_IsARefundUnderArticle18()
    {
        var (_, body) = FollowUpTemplate.BuildReminder(
            FollowUp(Facts(IncidentType.Cancellation, amount: 40m), LetterKind.Compensation));

        Assert.Contains("zwrot należności za bilet", body);
        Assert.Contains("art. 18 rozporządzenia (UE) 2021/782", body);
        Assert.DoesNotContain("art. 19", body);
        Assert.DoesNotContain("odszkodowania", body);
    }

    [Fact]
    public void Reminder_DocumentedCosts_ClaimsTheStoredCostsWithoutEuRules()
    {
        var (subject, body) = FollowUpTemplate.BuildReminder(
            FollowUp(Facts(amount: 0m, expense: 120m, carrier: "Polregio"), LetterKind.DocumentedCosts));

        Assert.Contains("wniosek o zwrot kosztów", subject);
        Assert.Contains("zwrot udokumentowanych kosztów w kwocie 120,00 zł", body);
        Assert.DoesNotContain("2021/782", body);
        Assert.DoesNotContain("art.", body);
        Assert.DoesNotContain("taksówka", body); // the passenger's free text stays in the original claim
    }

    [Fact]
    public void Reminder_CarrierRules_DemandsNoAmount()
    {
        var (subject, body) = FollowUpTemplate.BuildReminder(
            FollowUp(Facts(amount: 0m, carrier: "Polregio"), LetterKind.CarrierRules));

        Assert.Contains("reklamacja", subject);
        Assert.Contains("reklamację", body);
        Assert.Contains("regulamin", body);
        Assert.DoesNotContain("2021/782", body);
        Assert.DoesNotContain(" 0,00 zł", body);
        Assert.Contains("40,00 zł", body); // the ticket price, as a fact
    }

    [Fact]
    public void Reminder_WithoutFilingDate_LeavesAPlaceholder()
    {
        var (subject, body) = FollowUpTemplate.BuildReminder(
            new FollowUpFacts(Facts(), LetterKind.Compensation, null, true, false));

        Assert.Contains(FollowUpTemplate.FiledPlaceholder, subject);
        Assert.Contains(FollowUpTemplate.FiledPlaceholder, body);
    }

    [Fact]
    public void Utk_NoReply_IsAddressedToThePresidentAndCitesTheEuArticle()
    {
        var (subject, body) = FollowUpTemplate.BuildUtkComplaint(FollowUp(Facts(), LetterKind.Compensation));

        Assert.StartsWith("Skarga na przewoźnika PKP Intercity", subject);
        Assert.Contains("Prezes Urzędu Transportu Kolejowego", body);
        Assert.Contains("Al. Jerozolimskie 134", body);
        Assert.Contains("02-305 Warszawa", body);
        Assert.Contains("02.10.2026", body);
        Assert.Contains("nie otrzymałem(-am) odpowiedzi", body);
        Assert.DoesNotContain("nie uwzględnił", body);
        Assert.Contains("art. 19 rozporządzenia (UE) 2021/782", body);
        Assert.Contains("praw pasażerów", body);
        Assert.Contains("[kopia pisma do przewoźnika z dnia 02.10.2026]", body);
        Assert.Contains("[kopia biletu]", body);
        Assert.DoesNotContain("[kopia odpowiedzi przewoźnika]", body);
        // The regulator pays nothing: no payout sentence.
        Assert.DoesNotContain(ComplaintTemplate.Payout, body);
        Assert.Contains(ComplaintTemplate.FullName, body);
        Assert.Contains(ComplaintTemplate.Address, body);
    }

    [Fact]
    public void Utk_Rejected_SaysSoAndListsTheCarriersReply()
    {
        var (_, body) = FollowUpTemplate.BuildUtkComplaint(
            FollowUp(Facts(IncidentType.Cancellation, amount: 40m), LetterKind.Compensation, pastDeadline: false, rejected: true));

        Assert.Contains("nie uwzględnił", body);
        Assert.DoesNotContain("nie otrzymałem(-am) odpowiedzi", body);
        Assert.Contains("art. 18 rozporządzenia (UE) 2021/782", body);
        Assert.Contains("[kopia odpowiedzi przewoźnika]", body);
    }

    [Theory]
    [InlineData(LetterKind.DocumentedCosts)]
    [InlineData(LetterKind.CarrierRules)]
    public void Utk_OutsideTheEuScheme_RefersToCarriageLawWithoutArticles(LetterKind kind)
    {
        var (_, body) = FollowUpTemplate.BuildUtkComplaint(
            FollowUp(Facts(amount: 0m, expense: kind == LetterKind.DocumentedCosts ? 120m : null, carrier: "Polregio"), kind));

        Assert.DoesNotContain("2021/782", body);
        Assert.DoesNotContain("rozporządzeni", body);
        Assert.DoesNotContain("art.", body);
        Assert.Contains("prawa przewozowego", body);
        Assert.Contains("regulaminu przewozu", body);
        if (kind == LetterKind.DocumentedCosts)
            Assert.Contains("120,00 zł", body);
    }
}
