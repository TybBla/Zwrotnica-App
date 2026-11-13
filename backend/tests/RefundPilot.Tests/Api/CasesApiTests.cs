using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace RefundPilot.Tests.Api;

public sealed class CasesApiTests : IClassFixture<TestApiFactory>
{
    private readonly TestApiFactory _factory;

    public CasesApiTests(TestApiFactory factory) => _factory = factory;

    private async Task<HttpClient> LoggedInClientAsync(string email)
    {
        var client = _factory.CreateCookieClient();
        await AuthApiTests.RegisterAndLoginAsync(client, email);
        return client;
    }

    private async Task<HttpClient> DemoClientAsync()
    {
        var client = _factory.CreateCookieClient();
        var login = await client.PostAsJsonAsync("/api/auth/login?useCookies=true",
            new { email = TestApiFactory.DemoEmail, password = TestApiFactory.DemoPassword });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        return client;
    }

    private static object NewCase(string carrier = "PKP Intercity", string? category = "IC", int delay = 95,
        decimal price = 40m, string? cause = null, bool season = false) => new
        {
            carrier,
            trainCategory = category,
            fromStation = "Warszawa Centralna",
            toStation = "Kraków Główny",
            travelDate = "2026-11-01",
            ticketPrice = price,
            incidentType = "Delay",
            delayMinutes = delay,
            delayCause = cause,
            isSeasonTicket = season,
            payoutPreference = "BankTransfer",
        };

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage r) =>
        await r.Content.ReadFromJsonAsync<JsonElement>();

    private static async Task<JsonElement> CreateAsync(HttpClient client, object body)
    {
        var response = await client.PostAsJsonAsync("/api/cases", body);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await JsonAsync(response);
    }

    [Fact]
    public async Task Post_IntercityDelay_Returns201WithLocationAndComputedAmount()
    {
        var client = await LoggedInClientAsync("create@test.pl");

        var response = await client.PostAsJsonAsync("/api/cases", NewCase());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var json = await JsonAsync(response);
        Assert.EndsWith($"/api/cases/{json.GetProperty("id").GetInt32()}", response.Headers.Location!.AbsolutePath);
        Assert.Equal(10.00m, json.GetProperty("compensationAmount").GetDecimal());
        Assert.Equal("Eligible", json.GetProperty("verdict").GetString());
        Assert.Equal("New", json.GetProperty("status").GetString());
        Assert.False(json.TryGetProperty("userId", out _));
    }

    [Fact]
    public async Task Post_RegionalCarrier_IsNotCoveredWithZero()
    {
        var client = await LoggedInClientAsync("regional@test.pl");

        var json = await CreateAsync(client, NewCase(carrier: "Polregio", category: "R", delay: 200, price: 80m));

        Assert.Equal("NotCovered", json.GetProperty("verdict").GetString());
        Assert.Equal(0m, json.GetProperty("compensationAmount").GetDecimal());
    }

    [Fact]
    public async Task Post_SeasonTicket_IsUnknownWithZero()
    {
        var client = await LoggedInClientAsync("season@test.pl");

        var json = await CreateAsync(client, NewCase(season: true));

        Assert.Equal("Unknown", json.GetProperty("verdict").GetString());
        Assert.Equal(0m, json.GetProperty("compensationAmount").GetDecimal());
    }

    [Fact]
    public async Task Post_WeatherCause_SetsRefusalRiskButKeepsAmount()
    {
        var client = await LoggedInClientAsync("weathercase@test.pl");

        var json = await CreateAsync(client, NewCase(cause: "Weather"));

        Assert.True(json.GetProperty("refusalRisk").GetBoolean());
        Assert.Equal(10.00m, json.GetProperty("compensationAmount").GetDecimal());
    }

    [Fact]
    public async Task Post_EmptyCarrier_Returns400ProblemDetails()
    {
        var client = await LoggedInClientAsync("emptycarrier@test.pl");

        var response = await client.PostAsJsonAsync("/api/cases", NewCase(carrier: ""));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("problem+json", response.Content.Headers.ContentType!.MediaType);
    }

    [Fact]
    public async Task Post_NegativePrice_Returns400()
    {
        var client = await LoggedInClientAsync("negprice@test.pl");

        var response = await client.PostAsJsonAsync("/api/cases", NewCase(price: -1m));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Post_IntegerEnumValue_Returns400()
    {
        var client = await LoggedInClientAsync("intenum@test.pl");
        var body = new
        {
            carrier = "PKP Intercity", fromStation = "A", toStation = "B", travelDate = "2026-11-01",
            ticketPrice = 40, incidentType = "Delay", delayMinutes = 95, payoutPreference = 99,
        };

        var response = await client.PostAsJsonAsync("/api/cases", body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Get_UnknownId_Returns404()
    {
        var client = await LoggedInClientAsync("missing@test.pl");

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/cases/999999")).StatusCode);
    }

    [Fact]
    public async Task Put_RecomputesAssessmentServerSide()
    {
        var client = await LoggedInClientAsync("put@cases.pl");
        var created = await CreateAsync(client, NewCase());
        var id = created.GetProperty("id").GetInt32();

        var response = await client.PutAsJsonAsync($"/api/cases/{id}", NewCase(delay: 130, price: 100m));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(50.00m, (await JsonAsync(response)).GetProperty("compensationAmount").GetDecimal());
    }

    [Fact]
    public async Task Patch_Sent_SetsFiledAtAndResponseDueOneMonthLater()
    {
        var client = await LoggedInClientAsync("sent@test.pl");
        var id = (await CreateAsync(client, NewCase())).GetProperty("id").GetInt32();

        var response = await client.PatchAsJsonAsync($"/api/cases/{id}/status", new { status = "Sent" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await JsonAsync(response);
        var filed = json.GetProperty("filedAt").GetDateTime();
        Assert.Equal(_factory.Time.GetUtcNow().UtcDateTime, filed);
        Assert.Equal(filed.AddMonths(1), json.GetProperty("responseDueDate").GetDateTime());
    }

    [Fact]
    public async Task Patch_Paid_RaisesRecoveredStat()
    {
        var client = await LoggedInClientAsync("paid@test.pl");
        var id = (await CreateAsync(client, NewCase())).GetProperty("id").GetInt32();
        var before = await JsonAsync(await client.GetAsync("/api/stats"));
        Assert.Equal(0m, before.GetProperty("recovered").GetDecimal());
        Assert.Equal(10m, before.GetProperty("pending").GetDecimal());

        await client.PatchAsJsonAsync($"/api/cases/{id}/status", new { status = "Paid" });

        var after = await JsonAsync(await client.GetAsync("/api/stats"));
        Assert.Equal(10m, after.GetProperty("recovered").GetDecimal());
        Assert.Equal(0m, after.GetProperty("pending").GetDecimal());
        Assert.Equal(1, after.GetProperty("totalCases").GetInt32());
    }

    [Fact]
    public async Task Delete_Returns204ThenGet404()
    {
        var client = await LoggedInClientAsync("delete@test.pl");
        var id = (await CreateAsync(client, NewCase())).GetProperty("id").GetInt32();

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/cases/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/cases/{id}")).StatusCode);
    }

    [Fact]
    public async Task OtherUsersCase_Returns404ForAllVerbs_AndRowIsUnchanged()
    {
        var demo = await DemoClientAsync();
        var list = await JsonAsync(await demo.GetAsync("/api/cases"));
        var original = list[0];
        var id = original.GetProperty("id").GetInt32();
        var other = await LoggedInClientAsync("intruder@test.pl");

        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/cases/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await other.PutAsJsonAsync($"/api/cases/{id}", NewCase(carrier: "Hacked"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await other.PatchAsJsonAsync($"/api/cases/{id}/status", new { status = "Paid" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.DeleteAsync($"/api/cases/{id}")).StatusCode);

        var after = await JsonAsync(await demo.GetAsync($"/api/cases/{id}"));
        Assert.Equal(original.GetRawText(), after.GetRawText());
        Assert.Empty((await JsonAsync(await other.GetAsync("/api/cases"))).EnumerateArray());
    }

    [Fact]
    public async Task Anonymous_CasesAndStats_Return401()
    {
        var client = _factory.CreateCookieClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/cases")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/stats")).StatusCode);
    }

    private static object Assessment(decimal price = 40m, string? cause = null, bool season = false) => new
    {
        carrier = "PKP Intercity",
        trainCategory = "IC",
        incidentType = "Delay",
        delayMinutes = 95,
        ticketPrice = price,
        delayCause = cause,
        isSeasonTicket = season,
    };

    private async Task<JsonElement> AssessAsync(object body)
    {
        var response = await _factory.CreateClient().PostAsJsonAsync("/api/assessments", body);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await JsonAsync(response);
    }

    [Fact]
    public async Task Assessment_ZeroPrice_RequiresPrice()
    {
        var json = await AssessAsync(Assessment(price: 0m));

        Assert.True(json.GetProperty("priceRequired").GetBoolean());
    }

    [Fact]
    public async Task Assessment_CauseNeverChangesAmount_WeatherSetsRefusalRisk()
    {
        var none = await AssessAsync(Assessment());
        var weather = await AssessAsync(Assessment(cause: "Weather"));
        var strike = await AssessAsync(Assessment(cause: "RailStrike"));

        Assert.Equal(10m, none.GetProperty("compensationAmount").GetDecimal());
        Assert.Equal(10m, weather.GetProperty("compensationAmount").GetDecimal());
        Assert.True(weather.GetProperty("refusalRisk").GetBoolean());
        Assert.False(strike.GetProperty("refusalRisk").GetBoolean());
        Assert.False(none.GetProperty("refusalRisk").GetBoolean());
    }

    [Fact]
    public async Task Assessment_SeasonTicket_IsUnknownWithZero()
    {
        var json = await AssessAsync(Assessment(season: true));

        Assert.Equal("Unknown", json.GetProperty("verdict").GetString());
        Assert.Equal(0m, json.GetProperty("compensationAmount").GetDecimal());
    }

    private static Dictionary<string, object?> CaseBody(string? travelDate)
    {
        var body = new Dictionary<string, object?>
        {
            ["carrier"] = "PKP Intercity", ["fromStation"] = "A", ["toStation"] = "B", ["ticketPrice"] = 40m,
            ["incidentType"] = "Delay", ["delayMinutes"] = 95, ["isSeasonTicket"] = false,
            ["payoutPreference"] = "BankTransfer",
        };
        if (travelDate is not null)
            body["travelDate"] = travelDate;
        return body;
    }

    [Theory]
    [InlineData(null)]
    [InlineData("0001-01-01")]
    [InlineData("2026-11-14")]
    public async Task Post_MissingDefaultOrFutureTravelDate_Returns400(string? travelDate)
    {
        var client = await LoggedInClientAsync($"date{travelDate}@test.pl");

        var response = await client.PostAsJsonAsync("/api/cases", CaseBody(travelDate));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Put_FutureTravelDate_Returns400()
    {
        var client = await LoggedInClientAsync("putfuture@test.pl");
        var id = (await CreateAsync(client, NewCase())).GetProperty("id").GetInt32();

        var response = await client.PutAsJsonAsync($"/api/cases/{id}", CaseBody("2027-01-01"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Dates_AreSerializedAsUtcWithTrailingZ()
    {
        var client = await LoggedInClientAsync("utc@test.pl");
        var id = (await CreateAsync(client, NewCase())).GetProperty("id").GetInt32();
        await client.PatchAsJsonAsync($"/api/cases/{id}/status", new { status = "Sent" });

        var raw = await (await client.GetAsync($"/api/cases/{id}")).Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(raw).RootElement;
        foreach (var name in new[] { "createdAt", "filedAt", "responseDueDate" })
            Assert.EndsWith("Z", json.GetProperty(name).GetString());

        var list = await (await client.GetAsync("/api/cases")).Content.ReadAsStringAsync();
        Assert.EndsWith("Z", JsonDocument.Parse(list).RootElement[0].GetProperty("createdAt").GetString());

        var demo = await DemoClientAsync();
        var demoRaw = await (await demo.GetAsync("/api/cases")).Content.ReadAsStringAsync();
        foreach (var c in JsonDocument.Parse(demoRaw).RootElement.EnumerateArray())
        {
            Assert.EndsWith("Z", c.GetProperty("createdAt").GetString());
            if (c.GetProperty("responseDueDate").ValueKind == JsonValueKind.String)
                Assert.EndsWith("Z", c.GetProperty("responseDueDate").GetString());
        }
    }

    [Fact]
    public async Task Post_WithEmojiStation_Returns400()
    {
        var client = await LoggedInClientAsync("emojicase@test.pl");

        var response = await client.PostAsJsonAsync("/api/cases", new
        {
            carrier = "PKP Intercity",
            fromStation = "Warszawa 😀",
            toStation = "Kraków Główny",
            travelDate = "2026-11-01",
            ticketPrice = 40m,
            incidentType = "Delay",
            delayMinutes = 95,
            payoutPreference = "OriginalPayment",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("niedozwolone znaki", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Post_CanonicalizesStationAndNotes()
    {
        var client = await LoggedInClientAsync("canoncase@test.pl");

        var response = await client.PostAsJsonAsync("/api/cases", new
        {
            carrier = "PKP Intercity",
            fromStation = "Gdynia – Gdańsk",
            toStation = "Kraków Główny",
            travelDate = "2026-11-01",
            ticketPrice = 40m,
            incidentType = "Delay",
            delayMinutes = 95,
            payoutPreference = "OriginalPayment",
            notes = "Konduktor: „brak info”\r\nDrugi wiersz…",
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Gdynia - Gdańsk", json.GetProperty("fromStation").GetString());
        Assert.Equal("Konduktor: \"brak info\"\nDrugi wiersz...", json.GetProperty("notes").GetString());
    }
}
