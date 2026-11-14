using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RefundPilot.Infrastructure;

namespace RefundPilot.Tests.Infrastructure;

/// <summary>EnsureCreated never alters an existing file: databases created before the reminders get the new columns.</summary>
public sealed class SchemaUpgradeTests
{
    [Fact]
    public async Task AddsReminderColumns_ToADatabaseCreatedWithoutThem_KeepingRemindersOnForExistingUsers()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        db.Users.Add(new AppUser { Id = "old", UserName = "old@test.pl", Email = "old@test.pl" });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE Cases DROP COLUMN ReminderSentAt;");
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE AspNetUsers DROP COLUMN EmailReminders;");

        await SchemaUpgrade.ApplyAsync(db);
        await SchemaUpgrade.ApplyAsync(db); // idempotent: every start runs it

        Assert.True(await db.Users.Where(u => u.Id == "old").Select(u => u.EmailReminders).SingleAsync());
        Assert.Empty(await db.Cases.Where(c => c.ReminderSentAt != null).ToListAsync());
    }
}
