using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RefundPilot.Domain;

namespace RefundPilot.Infrastructure;

public static class DatabaseInitializer
{
    /// <summary>
    /// Creates the schema and, when demo credentials are configured, the demo user with its cases.
    /// <paramref name="useRollbackJournal"/> is for hosted file databases: Azure App Service keeps /home on an
    /// SMB share, where WAL's shared-memory index is unreliable and the rollback journal is not.
    /// </summary>
    public static async Task InitializeAsync(
        IServiceProvider services, string? demoEmail, string? demoPassword, bool useRollbackJournal = false)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var databaseFile = DatabaseFile(db.Database.GetConnectionString());
        if (databaseFile is not null)
            StorageDirectory.Ensure(Path.GetDirectoryName(databaseFile)!, "ConnectionStrings__Default",
                scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(DatabaseInitializer)));
        await db.Database.EnsureCreatedAsync();
        if (databaseFile is not null && useRollbackJournal)
            await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=DELETE;");

        if (string.IsNullOrWhiteSpace(demoEmail) || string.IsNullOrWhiteSpace(demoPassword))
            return;

        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        if (await users.FindByEmailAsync(demoEmail) is not null)
            return;

        // User and cases are separate tables: write them atomically.
        await using var transaction = await db.Database.BeginTransactionAsync();
        var demo = new AppUser
        {
            UserName = demoEmail,
            Email = demoEmail,
            EmailConfirmed = true,
            FullName = "Anna Kowalska",
            Address = "ul. Lipowa 12/4, 80-001 Gdańsk",
            Iban = new IbanCipher(scope.ServiceProvider.GetRequiredService<IDataProtectionProvider>())
                .Protect("PL61109010140000071219812874"),
            PreferredPayout = PayoutPreference.BankTransfer,
        };
        var result = await users.CreateAsync(demo, demoPassword);
        if (!result.Succeeded)
            throw new InvalidOperationException(
                "Demo user could not be created: " + string.Join("; ", result.Errors.Select(e => e.Description)));

        await SeedData.EnsureSeededAsync(db, demo.Id);
        await transaction.CommitAsync();
    }

    /// <summary>
    /// Full path of a file-backed database, or null for in-memory ones. SQLite creates the file but not
    /// its folder (e.g. /home/data on Azure App Service), so the caller creates the folder.
    /// </summary>
    private static string? DatabaseFile(string? connectionString)
    {
        var dataSource = new SqliteConnectionStringBuilder(connectionString).DataSource;
        if (string.IsNullOrEmpty(dataSource) || dataSource == ":memory:"
            || dataSource.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
            return null;
        return Path.GetFullPath(dataSource);
    }
}
