using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace RefundPilot.Tests.Api;

/// <summary>
/// Boots the API against a private shared-cache in-memory SQLite database. A sentinel connection is kept
/// open so the database lives as long as the factory; the app runs its normal startup seeding.
/// </summary>
public sealed class TestApiFactory : WebApplicationFactory<Program>
{
    public const string DemoEmail = "demo@refundpilot.test";
    public const string DemoPassword = "Demo!2026pass";

    public FakeTimeProvider Time { get; } = new(new DateTimeOffset(2026, 11, 13, 10, 0, 0, TimeSpan.Zero));

    private readonly SqliteConnection _sentinel;
    private readonly EphemeralDataProtectionProvider _dataProtection = new();
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
        builder.UseSetting("RateLimit:ExtractionsPerMinute", "100000");
        builder.UseSetting("Seed:DemoEmail", DemoEmail);
        builder.UseSetting("Seed:DemoPassword", DemoPassword);
        builder.ConfigureServices(services => services.AddSingleton<TimeProvider>(Time));
        // One in-memory key ring shared by every host built from this factory (they share the database).
        builder.ConfigureTestServices(services => services.AddSingleton<IDataProtectionProvider>(_dataProtection));
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
