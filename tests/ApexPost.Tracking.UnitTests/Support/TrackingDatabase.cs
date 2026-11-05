using ApexPost.Infrastructure.Tracking;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace ApexPost.Tracking.UnitTests.Support;

/// <summary>
/// A throw-away SQL Server database with only the tracking migrations applied, dropped when the fixture ends. The
/// server comes from ConnectionStrings:ApexPostTestServer (CI), or else from the web project's user-secrets; the
/// development database itself is never touched.
/// </summary>
public sealed class TrackingDatabase : IAsyncLifetime
{
    private const string TestServerName = "ApexPostTestServer";
    private const string WebUserSecretsId = "8ee05317-5b65-4866-a83b-e9e701628c18";

    public TrackingDatabase()
    {
        ConnectionString = new SqlConnectionStringBuilder(ServerConnection())
        {
            InitialCatalog = "ApexPostTests_Tracking_" + Guid.NewGuid().ToString("N"),
        }.ConnectionString;
    }

    public string ConnectionString { get; }

    public TrackingDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<TrackingDbContext>();
        TrackingDependencyInjection.UseTrackingSqlServer(options, ConnectionString);
        return new TrackingDbContext(options.Options);
    }

    public async ValueTask InitializeAsync()
    {
        await using var context = NewContext();
        await context.Database.MigrateAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await using var context = NewContext();
        await context.Database.EnsureDeletedAsync();
    }

    private static string ServerConnection()
    {
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets(WebUserSecretsId)
            .AddEnvironmentVariables()
            .Build();
        return configuration.GetConnectionString(TestServerName)
            ?? configuration.GetConnectionString(TrackingDependencyInjection.ConnectionName)
            ?? throw new InvalidOperationException(
                $"Set ConnectionStrings:{TestServerName}, or run setup-secrets.bat, to reach a SQL Server.");
    }
}
