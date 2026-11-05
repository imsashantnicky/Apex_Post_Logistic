using ApexPost.Api.Seeding;
using ApexPost.Api.Tests.Support;
using ApexPost.Application.Abstractions;
using ApexPost.Application.Tracking;
using ApexPost.Domain.Tracking;
using ApexPost.Infrastructure.Tracking;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ApexPost.Api.Tests.Hosting;

/// <summary>The host wires Week 3 and tracking services, and in Development migrates and seeds its database.</summary>
public sealed class DevelopmentHostTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Week_3_persistence_and_tracking_services_resolve_from_the_container()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;

        Assert.NotNull(services.GetRequiredService<IUnitOfWork>());
        Assert.NotNull(services.GetRequiredService<IBranchRepository>());
        Assert.NotNull(services.GetRequiredService<IClock>());
        Assert.NotNull(services.GetRequiredService<ITrackingUnitOfWork>());
        Assert.NotNull(services.GetRequiredService<IShipmentTrackingService>());
    }

    [Fact]
    public async Task Development_start_applies_every_tracking_migration()
    {
        await using var database = factory.OpenTrackingDatabase();

        Assert.Empty(await database.Database.GetPendingMigrationsAsync(Ct));
        Assert.Contains(
            await database.Database.GetAppliedMigrationsAsync(Ct),
            migration => migration.EndsWith("_AddShipmentTracking", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Development_seed_adds_ten_fictional_shipments_in_different_cities_and_statuses()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var tracking = scope.ServiceProvider.GetRequiredService<IShipmentTrackingService>();

        var page = await tracking.ListAsync(1, TrackingLimits.MaximumPageSize, null, null, Ct);
        var seeded = page.Items
            .Where(item => DevelopmentShipmentSeeder.TrackingNumbers.Contains(item.TrackingNumber))
            .ToList();

        Assert.Equal(10, seeded.Count);
        Assert.Equal("APX-20000001", seeded[0].TrackingNumber);
        Assert.Equal(10, seeded.Select(item => item.RecipientCity).Distinct().Count());
        Assert.Equal(Enum.GetValues<ShipmentStatus>().Order(), seeded.Select(item => item.Status).Distinct().Order());
    }

    [Fact]
    public async Task Seeding_again_adds_nothing()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var seeder = scope.ServiceProvider.GetRequiredService<DevelopmentShipmentSeeder>();

        Assert.Equal(0, await seeder.SeedAsync(Ct));
    }

    [Theory]
    [InlineData("appsettings.json")]
    [InlineData("appsettings.Development.json")]
    public async Task Settings_files_hold_no_connection_string(string fileName)
    {
        var contentRoot = factory.Services.GetRequiredService<IWebHostEnvironment>().ContentRootPath;

        var settings = await File.ReadAllTextAsync(Path.Combine(contentRoot, fileName), Ct);

        Assert.DoesNotContain("ConnectionStrings", settings, StringComparison.OrdinalIgnoreCase);
    }
}
