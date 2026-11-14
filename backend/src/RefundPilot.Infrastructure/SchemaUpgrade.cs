using Microsoft.EntityFrameworkCore;

namespace RefundPilot.Infrastructure;

/// <summary>
/// EnsureCreated builds a new database but never alters an existing one, so columns added after the first release
/// are added here (idempotent; runs on every start). A real product would use EF migrations instead.
/// </summary>
public static class SchemaUpgrade
{
    // Each ALTER is a complete constant statement (identifiers cannot be bound as parameters), never built from input.
    private static readonly (string Table, string Column, string AlterSql)[] AddedColumns =
    [
        ("Cases", "ReminderSentAt", "ALTER TABLE \"Cases\" ADD COLUMN \"ReminderSentAt\" TEXT NULL;"),
        ("AspNetUsers", "EmailReminders", "ALTER TABLE \"AspNetUsers\" ADD COLUMN \"EmailReminders\" INTEGER NOT NULL DEFAULT 1;"),
    ];

    public static async Task ApplyAsync(AppDbContext db, CancellationToken ct = default)
    {
        foreach (var (table, column, alterSql) in AddedColumns)
        {
            // SqlQuery turns the interpolated values into SQL parameters.
            var exists = await db.Database
                .SqlQuery<int>($"SELECT COUNT(*) AS Value FROM pragma_table_info({table}) WHERE name = {column}")
                .SingleAsync(ct) > 0;
            if (!exists)
                await db.Database.ExecuteSqlRawAsync(alterSql, ct);
        }
    }
}
