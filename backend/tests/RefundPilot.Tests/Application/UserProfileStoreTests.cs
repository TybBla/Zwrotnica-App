using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RefundPilot.Domain;
using RefundPilot.Infrastructure;

namespace RefundPilot.Tests.Application;

public sealed class UserProfileStoreTests : IAsyncLifetime
{
    private const string Plain = "PL61109010140000071219812874";
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private AppDbContext _db = null!;
    private UserProfileStore _store = null!;

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        _db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
        await _db.Database.EnsureCreatedAsync();
        _store = new UserProfileStore(_db, new EphemeralDataProtectionProvider());
        _db.Users.Add(new AppUser { Id = "u", UserName = "u", Email = "u@test.pl" });
        await _db.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
    }

    private Task<string?> RawIbanAsync() =>
        _db.Database.SqlQueryRaw<string?>("SELECT Iban AS Value FROM AspNetUsers WHERE Id = 'u'").SingleAsync();

    [Fact]
    public async Task Iban_IsEncryptedAtRestAndRoundTrips()
    {
        await _store.UpdateAsync("u", new UserProfile("A", "B", Plain, PayoutPreference.BankTransfer));

        var raw = await RawIbanAsync();
        Assert.NotNull(raw);
        Assert.NotEqual(Plain, raw);
        Assert.DoesNotContain(Plain, raw);
        Assert.Equal(Plain, (await _store.GetAsync("u"))!.Iban);
    }

    [Fact]
    public async Task NullIban_StaysNull()
    {
        await _store.UpdateAsync("u", new UserProfile("A", "B", null, PayoutPreference.OriginalPayment));

        Assert.Null(await RawIbanAsync());
        Assert.Null((await _store.GetAsync("u"))!.Iban);
    }

    [Fact]
    public async Task UndecryptableValue_ReadsAsNullWithoutThrowing()
    {
        await _db.Database.ExecuteSqlRawAsync("UPDATE AspNetUsers SET Iban = 'CfDJ8-garbage-or-lost-key' WHERE Id = 'u'");

        var profile = await _store.GetAsync("u");

        Assert.Null(profile!.Iban);
    }
}
