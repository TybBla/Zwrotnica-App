using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RefundPilot.Domain;

namespace RefundPilot.Infrastructure;

public static class DatabaseInitializer
{
    /// <summary>Creates the schema and, when demo credentials are configured, the demo user with its cases.</summary>
    public static async Task InitializeAsync(IServiceProvider services, string? demoEmail, string? demoPassword)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync();

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
}
