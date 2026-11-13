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
}
