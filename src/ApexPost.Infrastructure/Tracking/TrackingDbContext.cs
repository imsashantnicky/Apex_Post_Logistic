using ApexPost.Domain.Tracking;
using ApexPost.Infrastructure.Tracking.Configurations;
using Microsoft.EntityFrameworkCore;

namespace ApexPost.Infrastructure.Tracking;

/// <summary>
/// The EF Core model of shipment tracking. It shares the ApexPostDb database with the Week 3
/// <c>ApexPostDbContext</c> but owns only its two tables, its own migrations and the history table
/// <see cref="MigrationsHistoryTable"/>, so neither context ever migrates the tables of the other.
/// </summary>
public sealed class TrackingDbContext(DbContextOptions<TrackingDbContext> options) : DbContext(options)
{
    /// <summary>The table that records which tracking migrations ran.</summary>
    public const string MigrationsHistoryTable = "__TrackingMigrationsHistory";

    public DbSet<TrackedShipment> Shipments => Set<TrackedShipment>();

    public DbSet<TrackingScan> Scans => Set<TrackingScan>();

    /// <summary>Every DateTime is a datetime2 holding UTC, and comes back marked as UTC.</summary>
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        ArgumentNullException.ThrowIfNull(configurationBuilder);
        configurationBuilder.Properties<DateTime>()
            .HaveConversion<UtcDateTimeConverter>()
            .HaveColumnType(TrackingColumns.UtcTime);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.ApplyConfiguration(new TrackedShipmentConfiguration());
        modelBuilder.ApplyConfiguration(new TrackingScanConfiguration());
    }
}
