using ApexPost.Application.Services;
using ApexPost.Infrastructure.Persistence.Seeding;
using ApexPost.Infrastructure.Seeding;

namespace ApexPost.Web.Composition;

public static class ApplicationStartup
{
    /// <summary>
    /// Creates the notification subscriber, which listens from the moment it exists and is injected nowhere,
    /// and writes the demo data when the seeders allow it.
    /// </summary>
    public static async Task PrepareAsync(this WebApplication app)
    {
        var stopping = app.Lifetime.ApplicationStopping;
        _ = app.Services.GetRequiredService<NotificationSubscriber>();
        await app.Services.GetRequiredService<DemoDataSeeder>().SeedIfAllowedAsync(stopping);

        await using var scope = app.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<DevelopmentSeeder>().SeedIfAllowedAsync(stopping);
    }
}
