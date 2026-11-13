using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;

namespace RefundPilot.Tests.Api;

/// <summary>
/// Boots the API against a private shared-cache in-memory SQLite database. A sentinel connection is kept
/// open so the database lives as long as the factory; the app runs its normal startup seeding.
/// </summary>
public sealed class TestApiFactory : WebApplicationFactory<Program>
{
    public const string DemoEmail = "demo@refundpilot.test";
    public const string DemoPassword = "Demo!2026pass";

    private readonly SqliteConnection _sentinel;
    private readonly string _connectionString = $"DataSource=file:test-{Guid.NewGuid():N}?mode=memory&cache=shared";

    public TestApiFactory()
    {
        _sentinel = new SqliteConnection(_connectionString);
        _sentinel.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:Default", _connectionString);
        builder.UseSetting("Seed:DemoEmail", DemoEmail);
        builder.UseSetting("Seed:DemoPassword", DemoPassword);
    }

    public HttpClient CreateCookieClient() =>
        CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true, AllowAutoRedirect = false });

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            _sentinel.Dispose();
    }
}
