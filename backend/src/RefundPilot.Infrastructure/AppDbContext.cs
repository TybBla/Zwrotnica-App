using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using RefundPilot.Domain;

namespace RefundPilot.Infrastructure;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<AppUser>(options)
{
    public DbSet<RefundCase> Cases => Set<RefundCase>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // SQLite has no time zone support: always store and return UTC.
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
        configurationBuilder.Properties<DateTime?>().HaveConversion<NullableUtcDateTimeConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        var user = modelBuilder.Entity<AppUser>();
        user.Property(u => u.FullName).HasMaxLength(200);
        user.Property(u => u.Address).HasMaxLength(300);
        user.Property(u => u.Iban).HasMaxLength(34);
        user.Property(u => u.PreferredPayout).HasConversion<string>().HasMaxLength(20);

        var entity = modelBuilder.Entity<RefundCase>();
        entity.ToTable("Cases");
        entity.HasKey(c => c.Id);

        entity.Property(c => c.UserId).IsRequired();
        entity.HasIndex(c => c.UserId);
        entity.HasOne<AppUser>().WithMany().HasForeignKey(c => c.UserId).OnDelete(DeleteBehavior.Cascade);

        entity.Property(c => c.Carrier).IsRequired().HasMaxLength(100);
        entity.Property(c => c.TrainCategory).HasMaxLength(20);
        entity.Property(c => c.FromStation).IsRequired().HasMaxLength(100);
        entity.Property(c => c.ToStation).IsRequired().HasMaxLength(100);
        entity.Property(c => c.TicketNumber).HasMaxLength(50);
        entity.Property(c => c.Notes).HasMaxLength(2000);

        entity.Property(c => c.TicketPrice).HasPrecision(10, 2);
        entity.Property(c => c.CompensationAmount).HasPrecision(10, 2);

        entity.Property(c => c.IncidentType).HasConversion<string>().HasMaxLength(20);
        entity.Property(c => c.DelayCause).HasConversion<string>().HasMaxLength(20);
        entity.Property(c => c.Verdict).HasConversion<string>().HasMaxLength(20);
        entity.Property(c => c.PayoutPreference).HasConversion<string>().HasMaxLength(20);
        entity.Property(c => c.Status).HasConversion<string>().HasMaxLength(20);
    }

    private sealed class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
        v => v.Kind == DateTimeKind.Utc ? v : v.ToUniversalTime(),
        v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

    private sealed class NullableUtcDateTimeConverter() : ValueConverter<DateTime?, DateTime?>(
        v => v.HasValue && v.Value.Kind != DateTimeKind.Utc ? v.Value.ToUniversalTime() : v,
        v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v);
}
