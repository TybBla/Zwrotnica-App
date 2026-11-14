using Microsoft.EntityFrameworkCore;

namespace RefundPilot.Infrastructure;

/// <summary>
/// EnsureCreated builds a new database but never alters an existing one, so columns added after the first release
/// are added here (idempotent; runs on every start). A real product would use EF migrations instead.
/// </summary>
public static class SchemaUpgrade
{
    private static readonly (string Table, string Column, string Definition)[] AddedColumns =
    [
        ("Cases", "ReminderSentAt", "TEXT NULL"),
        ("AspNetUsers", "EmailReminders", "INTEGER NOT NULL DEFAULT 1"),
    ];

    public static async Task ApplyAsync(AppDbContext db, CancellationToken ct = default)
    {
        foreach (var (table, column, definition) in AddedColumns)
        {
            // Table and column names are the constants above, never user input.
            var exists = await db.Database
                .SqlQueryRaw<int>($"SELECT COUNT(*) AS Value FROM pragma_table_info('{table}') WHERE name = '{column}'")
                .SingleAsync(ct) > 0;
            if (!exists)
                await db.Database.ExecuteSqlRawAsync($"ALTER TABLE \"{table}\" ADD COLUMN \"{column}\" {definition};", ct);
        }
    }
}
