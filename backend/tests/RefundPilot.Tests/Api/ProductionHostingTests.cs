using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace RefundPilot.Tests.Api;

/// <summary>
/// Boots the API as in Azure (Production) with a stand-in built SPA in a temporary wwwroot, so the
/// SPA fallback can be checked against the API's own 404s.
/// </summary>
public sealed class ProductionApiFactory : WebApplicationFactory<Program>
{
    public const string IndexHtml = "<!doctype html><title>Zwrotnica</title><div id=\"root\"></div>";

    private readonly string _root = Path.Combine(Path.GetTempPath(), $"zwrotnica-prod-{Guid.NewGuid():N}");
    private readonly string _connectionString = $"DataSource=file:prod-{Guid.NewGuid():N}?mode=memory&cache=shared";
    private readonly SqliteConnection _sentinel;

    public ProductionApiFactory()
    {
        Directory.CreateDirectory(Path.Combine(_root, "wwwroot"));
        File.WriteAllText(Path.Combine(_root, "wwwroot", "index.html"), IndexHtml);
        _sentinel = new SqliteConnection(_connectionString);
        _sentinel.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");
        builder.UseSetting(WebHostDefaults.WebRootKey, Path.Combine(_root, "wwwroot"));
        builder.UseSetting("ConnectionStrings:Default", _connectionString);
        builder.UseSetting("DataProtection:KeysPath", Path.Combine(_root, "keys"));
        builder.ConfigureTestServices(services =>
            services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider()));
    }

    // Production cookies are Secure-only, so talk to the app over https like Azure's front end does.
    public HttpClient CreateHttpsClient() => CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"),
        HandleCookies = true,
        AllowAutoRedirect = false,
    });

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing)
            return;
        _sentinel.Dispose();
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { }
    }
}

public sealed class ProductionHostingTests : IClassFixture<ProductionApiFactory>
{
    private readonly ProductionApiFactory _factory;

    public ProductionHostingTests(ProductionApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Health_IsAnonymousAndOk()
    {
        var response = await _factory.CreateHttpsClient().GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("ok", json.GetProperty("status").GetString());
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/sprawy")]
    [InlineData("/sprawy/42")]
    public async Task ClientRoutes_ServeTheSpaIndex(string path)
    {
        var response = await _factory.CreateHttpsClient().GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(ProductionApiFactory.IndexHtml, await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("/api/does-not-exist")]
    [InlineData("/api")]
    [InlineData("/swagger")]
    [InlineData("/assets/missing.js")]
    public async Task ApiSwaggerAndMissingFiles_AreNotSwallowedByTheFallback(string path)
    {
        var response = await _factory.CreateHttpsClient().GetAsync(path);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.NotEqual(ProductionApiFactory.IndexHtml, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task UnknownApiRoute_ReturnsProblemDetails()
    {
        var response = await _factory.CreateHttpsClient().GetAsync("/api/does-not-exist");

        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(404, json.GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task ApiRoutes_StillWork()
    {
        var response = await _factory.CreateHttpsClient().GetAsync("/api/reference");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Login_IssuesASecureCookie()
    {
        var client = _factory.CreateHttpsClient();
        var credentials = new { email = $"prod-{Guid.NewGuid():N}@example.com", password = "Passw0rd!long" };
        (await client.PostAsJsonAsync("/api/auth/register", credentials)).EnsureSuccessStatusCode();

        var login = await client.PostAsJsonAsync("/api/auth/login?useCookies=true", credentials);

        login.EnsureSuccessStatusCode();
        var cookie = Assert.Single(login.Headers.GetValues("Set-Cookie"), c => c.StartsWith(".AspNetCore.Identity"));
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/profile")).StatusCode);
    }
}
