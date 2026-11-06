using ApexPost.Application.Tracking;
using ApexPost.Infrastructure.Persistence;
using ApexPost.Infrastructure.Tracking;
using ApexPost.Tracking.UnitTests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ApexPost.Tracking.UnitTests.Infrastructure;

/// <summary>The tracking model, read without a database: nothing here opens a connection.</summary>
public sealed class TrackingModelTests
{
    private const string UnusedConnection = "Server=unused;Database=unused";

    [Fact]
    public void The_model_has_the_two_tracking_tables_and_nothing_of_Week_3()
    {
        using var tracking = NewTrackingContext();
        using var week3 = new ApexPostDbContext(
            new DbContextOptionsBuilder<ApexPostDbContext>().UseSqlServer(UnusedConnection).Options);

        var trackingTables = tracking.Model.GetEntityTypes().Select(type => type.GetTableName()).ToHashSet();
        var week3Tables = week3.Model.GetEntityTypes().Select(type => type.GetTableName()).ToHashSet();

        Assert.Equal(["TrackedShipments", "TrackingScans"], trackingTables.Order());
        Assert.Empty(trackingTables.Intersect(week3Tables));
    }

    [Theory]
    [InlineData("TrackedShipments", "IX_TrackedShipments_TrackingNumber", "TrackingNumber")]
    [InlineData("TrackingScans", "IX_TrackingScans_ScanId", "ScanId")]
    public void Tracking_number_and_scan_id_have_unique_indexes(string table, string index, string column)
    {
        using var context = NewTrackingContext();
        var entity = context.Model.GetEntityTypes().Single(type => type.GetTableName() == table);

        var unique = entity.GetIndexes().Single(candidate => candidate.GetDatabaseName() == index);

        Assert.True(unique.IsUnique);
        Assert.Equal([column], unique.Properties.Select(property => property.Name));
    }

    [Fact]
    public void Migrations_use_their_own_history_table()
    {
        using var context = NewTrackingContext();

        var relational = RelationalOptionsExtension.Extract(context.GetService<IDbContextOptions>());

        Assert.Equal("__TrackingMigrationsHistory", relational.MigrationsHistoryTableName);
        Assert.Contains("__TrackingMigrationsHistory", context.GetService<IHistoryRepository>().GetCreateScript());
    }

    [Fact]
    public void The_migrations_match_the_model()
    {
        using var context = NewTrackingContext();

        Assert.NotEmpty(context.Database.GetMigrations());
        Assert.False(context.Database.HasPendingModelChanges());
    }

    [Fact]
    public void AddShipmentTracking_registers_every_port_as_scoped_and_validates()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"ConnectionStrings:{TrackingDependencyInjection.ConnectionName}"] = UnusedConnection,
            })
            .Build();
        var services = new ServiceCollection().AddShipmentTracking(configuration);
        services.AddScoped<ICurrentCourier, FixedCourier>();

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true,
        });
        using var scope = provider.CreateScope();

        Assert.IsType<ShipmentTrackingService>(scope.ServiceProvider.GetRequiredService<IShipmentTrackingService>());
        Assert.All(
            new[] { typeof(TrackingDbContext), typeof(ITrackedShipmentRepository), typeof(ITrackingUnitOfWork) },
            type => Assert.Equal(
                ServiceLifetime.Scoped,
                services.Single(descriptor => descriptor.ServiceType == type).Lifetime));
    }

    private static TrackingDbContext NewTrackingContext()
    {
        var options = new DbContextOptionsBuilder<TrackingDbContext>();
        TrackingDependencyInjection.UseTrackingSqlServer(options, UnusedConnection);
        return new TrackingDbContext(options.Options);
    }
}
