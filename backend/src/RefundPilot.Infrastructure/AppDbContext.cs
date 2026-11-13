using Microsoft.EntityFrameworkCore;
using RefundPilot.Domain;

namespace RefundPilot.Infrastructure;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<RefundCase> Cases => Set<RefundCase>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<RefundCase>();
        entity.ToTable("Cases");
        entity.HasKey(c => c.Id);

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
}
