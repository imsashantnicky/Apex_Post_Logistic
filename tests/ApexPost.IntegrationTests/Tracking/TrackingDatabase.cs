using ApexPost.Infrastructure.Tracking;
using ApexPost.Tests.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

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
    public TrackingDbContext NewContext() => NewContext([]);

    /// <summary>
    /// A new context on the test database with extra interceptors (for example <see cref="SqlCommandCapture"/>);
    /// skips the running test when no SQL Server was found.
    /// </summary>
    public TrackingDbContext NewContext(params IInterceptor[] interceptors)
    {
        SqlTestServer.SkipIfUnavailable();
        var options = new DbContextOptionsBuilder<TrackingDbContext>();
        TrackingDependencyInjection.UseTrackingSqlServer(options, ConnectionString);
        options.AddInterceptors(interceptors);
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
