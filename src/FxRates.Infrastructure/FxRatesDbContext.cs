using FxRates.Core;
using Microsoft.EntityFrameworkCore;

namespace FxRates.Infrastructure;

public class FxRatesDbContext : DbContext
{
    public FxRatesDbContext(DbContextOptions<FxRatesDbContext> options) : base(options) { }

    public DbSet<RateSnapshot> Snapshots => Set<RateSnapshot>();
    public DbSet<SourceRate> SourceRates => Set<SourceRate>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RateSnapshot>(e =>
        {
            e.HasKey(s => s.Id);
            e.Property(s => s.Base).HasMaxLength(8);
            e.Property(s => s.Quote).HasMaxLength(8);
            e.Property(s => s.Median).HasPrecision(18, 6);
            e.Property(s => s.Mean).HasPrecision(18, 6);
            e.Property(s => s.Min).HasPrecision(18, 6);
            e.Property(s => s.Max).HasPrecision(18, 6);
            e.HasIndex(s => s.AsOf);
            e.HasMany(s => s.Sources)
                .WithOne()
                .HasForeignKey(sr => sr.RateSnapshotId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SourceRate>(e =>
        {
            e.HasKey(sr => sr.Id);
            e.Property(sr => sr.Name).HasMaxLength(64);
            e.Property(sr => sr.Status).HasMaxLength(16);
            e.Property(sr => sr.Rate).HasPrecision(18, 6);
        });
    }
}
