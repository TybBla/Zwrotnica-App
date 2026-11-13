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

    private readonly TestApiFactory _factory;

    public ComplaintsApiTests(TestApiFactory factory) => _factory = factory;

    private async Task<HttpClient> LoggedInClientAsync(string email)
    {
        var client = _factory
            .WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddSingleton<IAiClient>(new OfflineAi())))
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
    public async Task Pdf_ReturnsAttachment()
    {
        var client = await LoggedInClientAsync("pdf@test.pl");
        var id = await CreateCaseAsync(client);

        var response = await client.PostAsJsonAsync($"/api/cases/{id}/complaint/pdf", new { payout = "OriginalPayment" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType!.MediaType);
        Assert.Equal("attachment", response.Content.Headers.ContentDisposition!.DispositionType);
        Assert.Equal($"reklamacja-{id}.pdf", response.Content.Headers.ContentDisposition.FileName);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.Equal("%PDF", Encoding.ASCII.GetString(bytes, 0, 4));
    }

    [Fact]
    public async Task OtherUsersCase_Returns404ForLetterAndPdf()
    {
        var owner = await LoggedInClientAsync("owner7@test.pl");
        var id = await CreateCaseAsync(owner);
        var intruder = await LoggedInClientAsync("intruder7@test.pl");

        Assert.Equal(HttpStatusCode.NotFound,
            (await intruder.PostAsJsonAsync($"/api/cases/{id}/complaint", new { payout = "Voucher" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await intruder.PostAsJsonAsync($"/api/cases/{id}/complaint/pdf", new { payout = "Voucher" })).StatusCode);
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
}
