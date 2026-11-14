using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RefundPilot.Application.Abstractions;
using RefundPilot.Infrastructure;

namespace RefundPilot.Tests.Api;

public sealed class ComplaintsApiTests : IClassFixture<TestApiFactory>
{
    private sealed class OfflineAi : IAiClient
    {
        public bool IsConfigured => false;
        public Task<string> GenerateJsonAsync(string p, IReadOnlyList<AiFile> f, string s, CancellationToken ct) =>
            throw new AiUnavailableException("off");
        public Task<string> GenerateTextAsync(string p, CancellationToken ct) => throw new AiUnavailableException("off");
    }

    private sealed class CountingAi : IAiClient
    {
        public int Calls { get; private set; }
        public bool IsConfigured => true;
        public Task<string> GenerateJsonAsync(string p, IReadOnlyList<AiFile> f, string s, CancellationToken ct)
        {
            Calls++;
            throw new AiUnavailableException("counted");
        }
        public Task<string> GenerateTextAsync(string p, CancellationToken ct) => throw new AiUnavailableException("counted");
    }

    private readonly TestApiFactory _factory;

    public ComplaintsApiTests(TestApiFactory factory) => _factory = factory;

    private async Task<HttpClient> LoggedInClientAsync(string email, IAiClient? ai = null)
    {
        var client = _factory
            .WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddSingleton<IAiClient>(ai ?? new OfflineAi())))
            .CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true, AllowAutoRedirect = false });
        await AuthApiTests.RegisterAndLoginAsync(client, email);
        return client;
    }

    private static async Task<int> CreateCaseAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/cases", new
        {
            carrier = "PKP Intercity", trainCategory = "IC", fromStation = "Warszawa Centralna",
            toStation = "Kraków Główny", ticketNumber = "IC-1", travelDate = "2026-11-01", ticketPrice = 40m,
            incidentType = "Delay", delayMinutes = 70, isSeasonTicket = false,
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
    }

    [Fact]
    public async Task Anonymous_Returns401()
    {
        var response = await _factory.CreateCookieClient().PostAsJsonAsync("/api/cases/1/complaint", new { payout = "Voucher" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Post_ReturnsTemplateLetterAndRemembersPayout()
    {
        var client = await LoggedInClientAsync("letter@test.pl");
        var id = await CreateCaseAsync(client);

        var response = await client.PostAsJsonAsync($"/api/cases/{id}/complaint", new { payout = "Voucher" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(json.GetProperty("usedAi").GetBoolean());
        Assert.Contains("w formie bonu", json.GetProperty("body").GetString());
        Assert.Contains("imię i nazwisko", json.GetProperty("missingFields").EnumerateArray().Select(e => e.GetString()));

        var profile = await (await client.GetAsync("/api/profile")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Voucher", profile.GetProperty("preferredPayout").GetString());
        var stored = await (await client.GetAsync($"/api/cases/{id}")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Voucher", stored.GetProperty("payoutPreference").GetString());
        Assert.Equal("Sent", stored.GetProperty("status").GetString());
        Assert.True(json.GetProperty("deadlineStarted").GetBoolean());
        Assert.Equal(stored.GetProperty("responseDueDate").GetDateTime(), json.GetProperty("responseDueDate").GetDateTime());
    }

    [Fact]
    public async Task Post_WithSavedOneTimeIban_ShowsInProfileButIsEncryptedInDatabase()
    {
        var client = await LoggedInClientAsync("iban@test.pl");
        var id = await CreateCaseAsync(client);
        const string iban = "PL27114020040000300201355387";

        var response = await client.PostAsJsonAsync($"/api/cases/{id}/complaint",
            new { payout = "BankTransfer", oneTimeIban = iban, saveIbanToProfile = true });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var profile = await (await client.GetAsync("/api/profile")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(iban, profile.GetProperty("iban").GetString());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var raw = await db.Database.SqlQueryRaw<string>(
            "SELECT Iban AS Value FROM AspNetUsers WHERE Email = 'iban@test.pl'").SingleAsync();
        Assert.NotEqual(iban, raw);
    }

    [Fact]
    public async Task SeededDemoIban_IsEncryptedInDatabase()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var raw = await db.Database.SqlQueryRaw<string>(
            "SELECT Iban AS Value FROM AspNetUsers WHERE Email = {0}", TestApiFactory.DemoEmail).SingleAsync();

        Assert.NotEqual("PL61109010140000071219812874", raw);
    }

    [Fact]
    public async Task Pdf_RendersPostedTextVerbatimWithoutAiOrWrites()
    {
        var ai = new CountingAi();
        var client = await LoggedInClientAsync("pdf@test.pl", ai);
        var id = await CreateCaseAsync(client);
        var callsBefore = ai.Calls;

        var response = await client.PostAsJsonAsync($"/api/cases/{id}/complaint/pdf",
            new { subject = "Temat ąęś", body = "Treść pisma\n\nz akapitami" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType!.MediaType);
        Assert.Equal("attachment", response.Content.Headers.ContentDisposition!.DispositionType);
        Assert.Equal($"reklamacja-{id}.pdf", response.Content.Headers.ContentDisposition.FileName);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.Equal("%PDF", Encoding.ASCII.GetString(bytes, 0, 4));
        Assert.Equal(callsBefore, ai.Calls);

        // No payout / profile side effects.
        var profile = await (await client.GetAsync("/api/profile")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("OriginalPayment", profile.GetProperty("preferredPayout").GetString());
    }

    [Fact]
    public async Task Pdf_OtherUsersCaseOrInvalidBody()
    {
        var owner = await LoggedInClientAsync("owner7@test.pl");
        var id = await CreateCaseAsync(owner);
        var intruder = await LoggedInClientAsync("intruder7@test.pl");
        var ok = new { subject = "S", body = "B" };

        Assert.Equal(HttpStatusCode.NotFound,
            (await intruder.PostAsJsonAsync($"/api/cases/{id}/complaint/pdf", ok)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await intruder.PostAsJsonAsync($"/api/cases/{id}/complaint", new { payout = "Voucher" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await owner.PostAsJsonAsync($"/api/cases/{id}/complaint/pdf", new { subject = "", body = "B" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await owner.PostAsJsonAsync($"/api/cases/{id}/complaint/pdf", new { subject = "S", body = new string('x', 6001) })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await owner.PostAsJsonAsync($"/api/cases/{id}/complaint/pdf", new { subject = new string('x', 201), body = "B" })).StatusCode);
    }

    [Theory]
    [InlineData("Polregio", null)]                // NotCovered: regional carrier
    [InlineData("Nieznany Przewoźnik", "X")]      // Unknown: carrier not recognised
    public async Task UncoveredCase_Returns200WithCarrierRulesComplaint(string carrier, string? category)
    {
        var client = await LoggedInClientAsync($"uncovered-{category ?? "none"}@test.pl");
        var created = await client.PostAsJsonAsync("/api/cases", new
        {
            carrier, trainCategory = category, fromStation = "A", toStation = "B", ticketNumber = "R-1",
            travelDate = "2026-11-01", ticketPrice = 40m, incidentType = "Delay", delayMinutes = 70,
            isSeasonTicket = false,
        });
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();

        var response = await client.PostAsJsonAsync($"/api/cases/{id}/complaint", new { payout = "Voucher" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.StartsWith("Reklamacja – opóźnienie pociągu", json.GetProperty("subject").GetString());
        Assert.Contains("regulamin", json.GetProperty("body").GetString());
        Assert.DoesNotContain("art. 19", json.GetProperty("body").GetString());
        var profile = await (await client.GetAsync("/api/profile")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Voucher", profile.GetProperty("preferredPayout").GetString());
    }

    [Fact]
    public async Task CoveredCaseWithNothingDue_Returns200WithCarrierRulesComplaint()
    {
        var client = await LoggedInClientAsync("noelig@test.pl");
        var created = await client.PostAsJsonAsync("/api/cases", new
        {
            carrier = "PKP Intercity", trainCategory = "IC", fromStation = "A", toStation = "B", ticketNumber = "IC-1",
            travelDate = "2026-11-01", ticketPrice = 40m, incidentType = "Delay", delayMinutes = 45,
            isSeasonTicket = false,
        });
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();

        var response = await client.PostAsJsonAsync($"/api/cases/{id}/complaint", new { payout = "Voucher" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.StartsWith("Reklamacja – opóźnienie pociągu PKP Intercity", json.GetProperty("subject").GetString());
        Assert.DoesNotContain("art. 19", json.GetProperty("body").GetString());
    }

    [Fact]
    public async Task CaseWithoutTicketPrice_Returns409ProblemAndKeepsPayout()
    {
        var client = await LoggedInClientAsync("noprice@test.pl");
        var created = await client.PostAsJsonAsync("/api/cases", new
        {
            carrier = "PKP Intercity", trainCategory = "IC", fromStation = "A", toStation = "B", ticketNumber = "IC-1",
            travelDate = "2026-11-01", ticketPrice = 0m, incidentType = "Delay", delayMinutes = 90,
            isSeasonTicket = false,
        });
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();

        var response = await client.PostAsJsonAsync($"/api/cases/{id}/complaint", new { payout = "Voucher" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("Podaj cenę biletu", await response.Content.ReadAsStringAsync());
        var profile = await (await client.GetAsync("/api/profile")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("OriginalPayment", profile.GetProperty("preferredPayout").GetString());
    }

    [Fact]
    public async Task TooLongIban_Returns400()
    {
        var client = await LoggedInClientAsync("long@test.pl");
        var id = await CreateCaseAsync(client);

        var response = await client.PostAsJsonAsync($"/api/cases/{id}/complaint",
            new { payout = "BankTransfer", oneTimeIban = new string('1', 35) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task OneTimeIban_IsNormalizedBeforeSaving()
    {
        var client = await LoggedInClientAsync("normiban@test.pl");
        var id = await CreateCaseAsync(client);

        var response = await client.PostAsJsonAsync($"/api/cases/{id}/complaint",
            new { payout = "BankTransfer", oneTimeIban = "27 1140 2004 0000 3002 0135 5387", saveIbanToProfile = true });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("PL27114020040000300201355387", await response.Content.ReadAsStringAsync());
        var profile = await (await client.GetAsync("/api/profile")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("PL27114020040000300201355387", profile.GetProperty("iban").GetString());
    }

    [Fact]
    public async Task OneTimeIban_WithBadChecksum_Returns400()
    {
        var client = await LoggedInClientAsync("badiban@test.pl");
        var id = await CreateCaseAsync(client);

        var response = await client.PostAsJsonAsync($"/api/cases/{id}/complaint",
            new { payout = "BankTransfer", oneTimeIban = "PL27114020040000300201355388" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Post_CaseWithoutStations_LetterHasPlaceholdersAndMissingFields()
    {
        var client = await LoggedInClientAsync("nostationsletter@test.pl");
        var created = await client.PostAsJsonAsync("/api/cases", new
        {
            carrier = "PKP Intercity", trainCategory = "IC", ticketNumber = "IC-1", travelDate = "2026-11-01", ticketPrice = 40m,
            incidentType = "Delay", delayMinutes = 70, isSeasonTicket = false,
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();

        var response = await client.PostAsJsonAsync($"/api/cases/{id}/complaint", new { payout = "Voucher" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains("[stacja początkowa]", json.GetProperty("body").GetString());
        Assert.Contains("[stacja docelowa]", json.GetProperty("body").GetString());
        var missing = json.GetProperty("missingFields").EnumerateArray().Select(m => m.GetString()).ToList();
        Assert.Contains("stacja początkowa", missing);
        Assert.Contains("stacja docelowa", missing);
    }
}
