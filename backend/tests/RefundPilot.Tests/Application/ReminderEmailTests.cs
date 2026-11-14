using RefundPilot.Application;
using RefundPilot.Domain;

namespace RefundPilot.Tests.Application;

public sealed class ReminderEmailTests
{
    private const string BaseUrl = "https://zwrotnica.example/";

    private static RefundCase Case(Verdict verdict = Verdict.Eligible, decimal amount = 25m) => new()
    {
        Id = 42,
        UserId = "u",
        Carrier = "PKP Intercity",
        TrainCategory = "IC",
        FromStation = "Gdynia Główna",
        ToStation = "Kraków Główny",
        TravelDate = new DateOnly(2026, 10, 1),
        TicketPrice = 100m,
        IncidentType = IncidentType.Delay,
        DelayMinutes = 70,
        Verdict = verdict,
        CompensationAmount = amount,
        Status = CaseStatus.Sent,
        FiledAt = new DateTime(2026, 10, 2, 9, 0, 0, DateTimeKind.Utc),
        ResponseDueDate = new DateTime(2026, 11, 2, 9, 0, 0, DateTimeKind.Utc),
    };

    [Fact]
    public void Compensation_ThanksHopesForThePayout_AndListsNextSteps()
    {
        var message = ReminderEmail.Build(Case(), BaseUrl);

        Assert.Contains("odszkodowanie", message.Subject);
        foreach (var part in new[] { message.Text, message.Html })
        {
            Assert.Contains("dziękujemy, że skorzystali Państwo ze Zwrotnicy", part);
            Assert.Contains("w wysokości 25,00 zł (według naszego wyliczenia)", part);
            Assert.Contains("kwoty, o którą Państwo wnioskowali", part);
            Assert.Contains("Wiadomość ma charakter informacyjny i nie stanowi porady prawnej.", part);
            Assert.Contains("PKP Intercity", part);
            Assert.Contains("01.10.2026", part);
            Assert.Contains("02.11.2026", part);
            Assert.Contains("ponaglenie", part);
            Assert.Contains("Prezesa Urzędu Transportu Kolejowego", part);
            Assert.Contains("https://utk.gov.pl", part);
            Assert.Contains("Rzecznika Praw Pasażera Kolei", part);
            Assert.Contains("https://zwrotnica.example/sprawy/42", part);
            Assert.Contains("https://zwrotnica.example/konto", part);
        }
    }

    [Theory]
    [InlineData(Verdict.NotCovered, 0)]
    [InlineData(Verdict.Unknown, 0)]
    [InlineData(Verdict.Eligible, 0)]
    public void CarrierRulesComplaint_AsksAboutTheAnswer_WithoutClaimingMoneyIsDue(Verdict verdict, int amount)
    {
        var message = ReminderEmail.Build(Case(verdict, amount), BaseUrl);

        Assert.Contains("reklamację", message.Subject);
        foreach (var part in new[] { message.Subject, message.Text, message.Html })
        {
            Assert.DoesNotContain("odszkodowani", part);
            Assert.DoesNotContain("należn", part);
            Assert.DoesNotMatch(@"\d zł", part);
        }
        Assert.Contains("odpowiedź na reklamację", message.Text);
        Assert.Contains("Wiadomość ma charakter informacyjny i nie stanowi porady prawnej.", message.Text);
        Assert.Contains("ponaglenie", message.Text);
        Assert.Contains("https://utk.gov.pl", message.Text);
        Assert.Contains("https://zwrotnica.example/sprawy/42", message.Html);
    }

    [Fact]
    public void RegionalCaseWithCosts_RefersToTheCostsClaim()
    {
        var c = Case(Verdict.NotCovered, 0);
        c.ExpenseAmount = 85m;

        var message = ReminderEmail.Build(c, BaseUrl);

        Assert.Equal("Czy przewoźnik odpowiedział na wniosek o zwrot kosztów? – Zwrotnica", message.Subject);
        foreach (var part in new[] { message.Text, message.Html })
        {
            Assert.Contains("Według naszych zapisów wniosek o zwrot kosztów do przewoźnika PKP Intercity", part);
            Assert.Contains("odpowiedź na wniosek o zwrot kosztów", part);
            Assert.Contains("powołuje się na wniosek o zwrot kosztów i datę wysłania", part);
            Assert.DoesNotContain("reklamac", part);
            Assert.DoesNotContain("odszkodowani", part);
        }
    }

    [Fact]
    public void Html_EncodesTextTheUserTypedIn()
    {
        var c = Case();
        c.Carrier = "<script>alert(1)</script> & Co";

        var message = ReminderEmail.Build(c, BaseUrl);

        Assert.DoesNotContain("<script>", message.Html);
        Assert.Contains("&lt;script&gt;", message.Html);
        Assert.Contains("<script>", message.Text);
    }

    [Fact]
    public void CancelledTrain_AsksAboutTheTicketRefund()
    {
        var c = Case();
        c.IncidentType = IncidentType.Cancellation;
        c.CompensationAmount = 100m;

        var message = ReminderEmail.Build(c, BaseUrl);

        Assert.Contains("należność za bilet", message.Subject);
        Assert.Contains("Jeżeli przewoźnik nie zwrócił należności za bilet", message.Text);
        Assert.DoesNotContain("odszkodowani", message.Text);
    }
}
