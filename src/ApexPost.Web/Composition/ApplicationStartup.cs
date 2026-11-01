using ApexPost.Application.Services;
using ApexPost.Infrastructure.Seeding;

namespace ApexPost.Web.Composition;

public static class ApplicationStartup
{
    /// <summary>
    /// Creates the notification subscriber, which listens from the moment it exists and is injected nowhere,
    /// and writes the demo data when the seeder allows it.
    /// </summary>
    public static async Task PrepareAsync(this WebApplication app)
    {
        _ = app.Services.GetRequiredService<NotificationSubscriber>();
        await app.Services.GetRequiredService<DemoDataSeeder>().SeedIfAllowedAsync(app.Lifetime.ApplicationStopping);
    }
}
