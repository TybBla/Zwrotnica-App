using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RefundPilot.Infrastructure;

namespace RefundPilot.Tests.Api;

/// <summary>Ponaglenie and skarga do UTK: templates only, available by case status and the Warsaw deadline.</summary>
public sealed class FollowUpLettersApiTests : IClassFixture<TestApiFactory>
{
    private readonly TestApiFactory _factory;

    public FollowUpLettersApiTests(TestApiFactory factory) => _factory = factory;

    private async Task<HttpClient> LoggedInAsync(string email)
    {
        var client = _factory.CreateCookieClient();
        await AuthApiTests.RegisterAndLoginAsync(client, email);
        return client;
    }

    private static async Task<int> CreateCaseAsync(HttpClient client, string? status = null)
    {
        var response = await client.PostAsJsonAsync("/api/cases", new
        {
            carrier = "PKP Intercity", trainCategory = "IC", fromStation = "Warszawa Centralna",
            toStation = "Kraków Główny", ticketNumber = "IC-77", travelDate = "2026-11-01", ticketPrice = 40m,
            incidentType = "Delay", delayMinutes = 70, isSeasonTicket = false,
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var id = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
        if (status is not null)
            Assert.Equal(HttpStatusCode.OK, (await client.PatchAsJsonAsync($"/api/cases/{id}/status", new { status })).StatusCode);
        return id;
    }

    /// <summary>Moves the carrier's deadline (and the filing a month before it) to the given UTC instant.</summary>
    private async Task SetDeadlineAsync(int id, DateTime dueUtc)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var refundCase = await db.Cases.SingleAsync(c => c.Id == id);
        refundCase.ResponseDueDate = dueUtc;
        refundCase.FiledAt = dueUtc.AddMonths(-1);
        await db.SaveChangesAsync();
    }

    private static Task<HttpResponseMessage> Generate(HttpClient client, int id, string kind) =>
        client.PostAsync($"/api/cases/{id}/letters/{kind}", null);

    private static async Task<JsonElement> Json(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    [Fact]
    public async Task Anonymous_Returns401()
    {
        var response = await _factory.CreateCookieClient().PostAsync("/api/cases/1/letters/reminder", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task NewCase_BothLettersAre409WithAPolishMessage()
    {
        var client = await LoggedInAsync("fu-new@test.pl");
        var id = await CreateCaseAsync(client);

        foreach (var kind in new[] { "reminder", "utk-complaint" })
        {
            var response = await Generate(client, id, kind);
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
            Assert.Contains("Wysłana", (await Json(response)).GetProperty("detail").GetString());
        }

        var item = await client.GetFromJsonAsync<JsonElement>($"/api/cases/{id}");
        Assert.False(item.GetProperty("followUps").GetProperty("reminder").GetBoolean());
        Assert.False(item.GetProperty("followUps").GetProperty("utkComplaint").GetBoolean());
    }

    [Fact]
    public async Task SentBeforeTheDeadline_ReminderOnly()
    {
        var client = await LoggedInAsync("fu-sent@test.pl");
        var id = await CreateCaseAsync(client, "Sent");

        var reminder = await Generate(client, id, "reminder");
        Assert.Equal(HttpStatusCode.OK, reminder.StatusCode);
        var letter = await Json(reminder);
        Assert.StartsWith("Ponaglenie", letter.GetProperty("subject").GetString());
        var body = letter.GetProperty("body").GetString()!;
        Assert.Contains("13.11.2026", body); // filed today (the factory's clock)
        Assert.Contains("bilet nr IC-77", body);
        Assert.Contains("10,00 zł", body);
        Assert.DoesNotContain("upłynął", body);
        Assert.Contains("na rachunek bankowy lub kartę", body); // the case's payout sentence
        Assert.DoesNotContain("{{", body);
        Assert.False(letter.GetProperty("usedAi").GetBoolean());
        Assert.Contains("imię i nazwisko", letter.GetProperty("missingFields").EnumerateArray().Select(m => m.GetString()));
        Assert.False(letter.GetProperty("deadlineStarted").GetBoolean());

        var utk = await Generate(client, id, "utk-complaint");
        Assert.Equal(HttpStatusCode.Conflict, utk.StatusCode);
        Assert.Contains("termin", (await Json(utk)).GetProperty("detail").GetString());

        var item = await client.GetFromJsonAsync<JsonElement>($"/api/cases/{id}");
        Assert.True(item.GetProperty("followUps").GetProperty("reminder").GetBoolean());
        Assert.False(item.GetProperty("followUps").GetProperty("utkComplaint").GetBoolean());
        Assert.False(item.GetProperty("followUps").GetProperty("pastDeadline").GetBoolean());
    }

    [Fact]
    public async Task SentPastTheWarsawDeadline_BothLettersAndNoWrites()
    {
        var client = await LoggedInAsync("fu-late@test.pl");
        var id = await CreateCaseAsync(client, "Sent");
        // The factory's "now" is 13.11.2026 10:00 UTC; a deadline on 12.11 (Warsaw) has passed.
        await SetDeadlineAsync(id, new DateTime(2026, 11, 12, 10, 0, 0, DateTimeKind.Utc));
        var before = await client.GetFromJsonAsync<JsonElement>($"/api/cases/{id}");
        Assert.True(before.GetProperty("followUps").GetProperty("pastDeadline").GetBoolean());
        Assert.True(before.GetProperty("followUps").GetProperty("utkComplaint").GetBoolean());

        var reminder = await Json(await Generate(client, id, "reminder"));
        Assert.Contains("upłynął", reminder.GetProperty("body").GetString());

        var utkResponse = await Generate(client, id, "utk-complaint");
        Assert.Equal(HttpStatusCode.OK, utkResponse.StatusCode);
        var utk = await Json(utkResponse);
        var body = utk.GetProperty("body").GetString()!;
        Assert.Contains("Prezes Urzędu Transportu Kolejowego", body);
        Assert.Contains("12.10.2026", body); // the original filing
        Assert.Contains("art. 19 rozporządzenia (UE) 2021/782", body);
        Assert.Contains("załączniki", string.Join(",", utk.GetProperty("missingFields").EnumerateArray().Select(m => m.GetString())));

        // Generating follow-ups never changes the case.
        var after = await client.GetFromJsonAsync<JsonElement>($"/api/cases/{id}");
        Assert.Equal("Sent", after.GetProperty("status").GetString());
        Assert.Equal(before.GetProperty("filedAt").GetString(), after.GetProperty("filedAt").GetString());
    }

    [Fact]
    public async Task DeadlineEndingTodayInWarsaw_IsNotPastYet()
    {
        var client = await LoggedInAsync("fu-today@test.pl");
        var id = await CreateCaseAsync(client, "Sent");
        // 12.11 23:30 UTC is 13.11 00:30 in Warsaw: the carrier still has today.
        await SetDeadlineAsync(id, new DateTime(2026, 11, 12, 23, 30, 0, DateTimeKind.Utc));

        Assert.Equal(HttpStatusCode.Conflict, (await Generate(client, id, "utk-complaint")).StatusCode);
    }

    [Fact]
    public async Task Rejected_UtkComplaintOnly()
    {
        var client = await LoggedInAsync("fu-rejected@test.pl");
        var id = await CreateCaseAsync(client, "Sent");
        await client.PatchAsJsonAsync($"/api/cases/{id}/status", new { status = "Rejected" });

        Assert.Equal(HttpStatusCode.Conflict, (await Generate(client, id, "reminder")).StatusCode);
        var utk = await Json(await Generate(client, id, "utk-complaint"));
        Assert.Contains("nie uwzględnił", utk.GetProperty("body").GetString());
    }

    [Fact]
    public async Task OtherUsersCase_UnknownKindOrMissingCase_Return404()
    {
        var owner = await LoggedInAsync("fu-owner@test.pl");
        var id = await CreateCaseAsync(owner, "Sent");
        var intruder = await LoggedInAsync("fu-intruder@test.pl");
        var pdf = new { subject = "S", body = "B" };

        Assert.Equal(HttpStatusCode.NotFound, (await Generate(intruder, id, "reminder")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await intruder.PostAsJsonAsync($"/api/cases/{id}/letters/reminder/pdf", pdf)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Generate(owner, id, "appeal")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Generate(owner, 999_999, "reminder")).StatusCode);
    }

    [Fact]
    public async Task Pdf_RendersThePostedTextUnderAKindSpecificName()
    {
        var client = await LoggedInAsync("fu-pdf@test.pl");
        var id = await CreateCaseAsync(client, "Sent");

        var response = await client.PostAsJsonAsync($"/api/cases/{id}/letters/reminder/pdf",
            new { subject = "Ponaglenie ąę", body = "Treść\n\nakapit" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType!.MediaType);
        Assert.Equal($"ponaglenie-{id}.pdf", response.Content.Headers.ContentDisposition!.FileName);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.Equal("%PDF", Encoding.ASCII.GetString(bytes, 0, 4));

        // Not available yet (deadline running) and invalid bodies.
        Assert.Equal(HttpStatusCode.Conflict,
            (await client.PostAsJsonAsync($"/api/cases/{id}/letters/utk-complaint/pdf", new { subject = "S", body = "B" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PostAsJsonAsync($"/api/cases/{id}/letters/reminder/pdf", new { subject = "", body = "B" })).StatusCode);
    }

    [Fact]
    public async Task Pdf_UtkComplaintFileName()
    {
        var client = await LoggedInAsync("fu-pdf-utk@test.pl");
        var id = await CreateCaseAsync(client, "Sent");
        await client.PatchAsJsonAsync($"/api/cases/{id}/status", new { status = "Rejected" });

        var response = await client.PostAsJsonAsync($"/api/cases/{id}/letters/utk-complaint/pdf", new { subject = "Skarga", body = "Treść" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal($"skarga-utk-{id}.pdf", response.Content.Headers.ContentDisposition!.FileName);
    }
}
