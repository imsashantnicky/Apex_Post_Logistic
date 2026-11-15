using ApexPost.Infrastructure.Tracking;
using ApexPost.Tests.Common;
using Microsoft.EntityFrameworkCore;

namespace ApexPost.IntegrationTests.Tracking;

/// <summary>
/// A throw-away SQL Server database with only the tracking migrations applied, dropped when the fixture ends. The
/// server comes from <see cref="SqlTestServer"/>; without one, nothing is created and the first
/// <see cref="NewContext"/> call inside a test skips that test.
/// </summary>
public sealed class TrackingDatabase : IAsyncLifetime
{
    public string ConnectionString { get; } = SqlTestServer.NewDatabase("Tracking");

    /// <summary>A new context on the test database; skips the running test when no SQL Server was found.</summary>
    public TrackingDbContext NewContext()
    {
        SqlTestServer.SkipIfUnavailable();
        var options = new DbContextOptionsBuilder<TrackingDbContext>();
        TrackingDependencyInjection.UseTrackingSqlServer(options, ConnectionString);
        return new TrackingDbContext(options.Options);
    }

    public async ValueTask InitializeAsync()
    {
        if (!SqlTestServer.IsAvailable)
        {
            return;
        }

        await using var context = NewContext();
        await context.Database.MigrateAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (!SqlTestServer.IsAvailable)
        {
            return;
        }

        await using var context = NewContext();
        await context.Database.EnsureDeletedAsync();
    }
}
