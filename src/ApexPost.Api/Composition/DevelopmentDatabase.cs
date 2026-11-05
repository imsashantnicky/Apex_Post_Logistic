using ApexPost.Api.Seeding;
using ApexPost.Infrastructure.Tracking;
using Microsoft.EntityFrameworkCore;

namespace ApexPost.Api.Composition;

/// <summary>Prepares a developer's database when the API starts in Development.</summary>
public static class DevelopmentDatabase
{
    /// <summary>
    /// Applies the tracking migrations (never the Week 3 ones, which have their own history table) and adds the
    /// fictional demo shipments that are missing.
    /// </summary>
    public static async Task PrepareDevelopmentDatabaseAsync(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        var stopping = app.Lifetime.ApplicationStopping;

        await using var scope = app.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<TrackingDbContext>().Database.MigrateAsync(stopping);
        await scope.ServiceProvider.GetRequiredService<DevelopmentShipmentSeeder>().SeedAsync(stopping);
    }
}
