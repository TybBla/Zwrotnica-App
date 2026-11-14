using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace RefundPilot.Tests.Api;

public sealed class ProfileApiTests : IClassFixture<TestApiFactory>
{
    private readonly TestApiFactory _factory;

    public ProfileApiTests(TestApiFactory factory) => _factory = factory;

    private async Task<HttpClient> LoggedInClientAsync(string email)
    {
        var client = _factory.CreateCookieClient();
        await AuthApiTests.RegisterAndLoginAsync(client, email);
        return client;
    }

    [Fact]
    public async Task Get_ReturnsEmailOfLoggedInUser()
    {
        var client = await LoggedInClientAsync("get@test.pl");

        var response = await client.GetAsync("/api/profile");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("get@test.pl", json.GetProperty("email").GetString());
    }

    [Fact]
    public async Task Put_PersistsProfile_IncludingVoucherPayout()
    {
        var client = await LoggedInClientAsync("put@test.pl");

        var put = await client.PutAsJsonAsync("/api/profile", new
        {
            fullName = "Jan Testowy",
            address = "ul. Przykładowa 1, 00-001 Warszawa",
            iban = "PL61109010140000071219812874",
            preferredPayout = "Voucher",
        });
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);

        var json = await (await client.GetAsync("/api/profile")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Jan Testowy", json.GetProperty("fullName").GetString());
        Assert.Equal("PL61109010140000071219812874", json.GetProperty("iban").GetString());
        Assert.Equal("Voucher", json.GetProperty("preferredPayout").GetString());
    }

    [Fact]
    public async Task Put_WithTooLongIban_Returns400()
    {
        var client = await LoggedInClientAsync("iban@test.pl");

        var response = await client.PutAsJsonAsync("/api/profile",
            new { iban = new string('1', 35), preferredPayout = "BankTransfer" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Put_WithTooLongName_Returns400()
    {
        var client = await LoggedInClientAsync("name@test.pl");

        var response = await client.PutAsJsonAsync("/api/profile",
            new { fullName = new string('a', 201), preferredPayout = "BankTransfer" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Put_Anonymous_Returns401()
    {
        var response = await _factory.CreateCookieClient()
            .PutAsJsonAsync("/api/profile", new { preferredPayout = "BankTransfer" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Put_WithEmojiName_Returns400NamingTheField()
    {
        var client = await LoggedInClientAsync("emoji@test.pl");

        var response = await client.PutAsJsonAsync("/api/profile", new { fullName = "Jan 😀", preferredPayout = "Voucher" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("niedozwolone znaki", body);
        Assert.Contains("FullName", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Put_NormalizesIban()
    {
        var client = await LoggedInClientAsync("norm@test.pl");

        var put = await client.PutAsJsonAsync("/api/profile",
            new { iban = "pl61 1090 1014 0000 0712 1981 2874", preferredPayout = "BankTransfer" });
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);

        var json = await (await client.GetAsync("/api/profile")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("PL61109010140000071219812874", json.GetProperty("iban").GetString());
    }

    [Fact]
    public async Task Put_WithBadChecksumIban_Returns400()
    {
        var client = await LoggedInClientAsync("badsum@test.pl");

        var response = await client.PutAsJsonAsync("/api/profile",
            new { iban = "PL61109010140000071219812875", preferredPayout = "BankTransfer" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Put_CanonicalizesTextAndIbanBeforeStoring()
    {
        var client = await LoggedInClientAsync("canon@test.pl");

        var put = await client.PutAsJsonAsync("/api/profile", new
        {
            fullName = "Jan  O’Neil",
            address = "ul. Długa 5\u00A0/ 12",
            iban = "PL61\u00A01090 1014 0000 0712 1981 2874",
            preferredPayout = "BankTransfer",
        });
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);

        var json = await (await client.GetAsync("/api/profile")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Jan O'Neil", json.GetProperty("fullName").GetString());
        Assert.Equal("ul. Długa 5 / 12", json.GetProperty("address").GetString());
        Assert.Equal("PL61109010140000071219812874", json.GetProperty("iban").GetString());
    }

    [Fact]
    public async Task Put_ComposesDecomposedLetters()
    {
        var client = await LoggedInClientAsync("nfd@test.pl");

        await client.PutAsJsonAsync("/api/profile", new { fullName = "Zo\u0301ła", preferredPayout = "Voucher" });

        var json = await (await client.GetAsync("/api/profile")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Zóła", json.GetProperty("fullName").GetString());
    }

    [Fact]
    public async Task EmailReminders_AreOnByDefault_AndTheOptOutPersists()
    {
        var client = await LoggedInClientAsync("reminders@test.pl");

        var initial = await (await client.GetAsync("/api/profile")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(initial.GetProperty("emailReminders").GetBoolean());

        var put = await client.PutAsJsonAsync("/api/profile", new { preferredPayout = "Voucher", emailReminders = false });
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        Assert.False((await put.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("emailReminders").GetBoolean());

        var json = await (await client.GetAsync("/api/profile")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(json.GetProperty("emailReminders").GetBoolean());
    }
}
