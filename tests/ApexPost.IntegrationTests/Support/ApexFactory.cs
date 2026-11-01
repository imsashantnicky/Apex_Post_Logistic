using ApexPost.Infrastructure.Persistence;
using ApexPost.Web.Composition;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ApexPost.IntegrationTests.Support;

/// <summary>
/// The real application on a throw-away SQL Server database of its own, migrated and seeded, with fast password
/// hashing, known partner keys, a short courier timeout, captured logs and a remote address chosen per client. The
/// server comes from ConnectionStrings:ApexPostTestServer, or else from the application's own user-secrets; the
/// application's database itself is never touched, and the temporary one is dropped with the factory.
/// </summary>
public class ApexFactory : WebApplicationFactory<Program>
{
    public const string DemoPassword = "Demo-password-for-tests";
    public const string PartnerKey = "partner-key-for-tests";
    public const string BurstPartnerKey = "burst-partner-key-for-tests";
    public const string PartnerOrigin = "https://tracking.partner.example";

    private const string TestServerName = "ApexPostTestServer";
    private const string ApplicationDatabaseName = "ApexPostDb";

    private bool _databaseCreated;

    public ApexFactory()
    {
        var server = new SqlConnectionStringBuilder(ServerConnection())
        {
            InitialCatalog = "ApexPostTests_" + Guid.NewGuid().ToString("N"),
        };
        ConnectionString = server.ConnectionString;
    }

    public string ConnectionString { get; }

    public CapturedLogs Logs { get; } = new();

    protected virtual string EnvironmentName => Environments.Development;

    protected virtual IEnumerable<KeyValuePair<string, string?>> ExtraSettings => [];

    /// <summary>A context for reading or changing the data behind a test's back; starts the host first.</summary>
    public ApexPostDbContext OpenDatabase()
    {
        _ = Services;
        return NewContext();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(EnvironmentName);
        builder.ConfigureLogging(logging => logging.AddProvider(Logs));
        builder.ConfigureServices(services => services.AddSingleton<IStartupFilter, RemoteAddressStartupFilter>());
        builder.ConfigureAppConfiguration((_, configuration) => configuration
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Seed:Enabled"] = "true",
                ["Seed:DemoPassword"] = DemoPassword,
                [$"ConnectionStrings:{ApplicationDatabaseName}"] = ConnectionString,
                ["Passwords:Iterations"] = "1000",
                ["Courier:TimeoutMilliseconds"] = "300",
                ["Integration:ApiKeys:Acme"] = PartnerKey,
                ["Integration:ApiKeys:Burst"] = BurstPartnerKey,
            })
            .AddInMemoryCollection(ExtraSettings));
    }

    /// <summary>The application never migrates by itself, so the database must exist before the host starts.</summary>
    protected override IHost CreateHost(IHostBuilder builder)
    {
        using var database = NewContext();
        database.Database.Migrate();
        _databaseCreated = true;
        return base.CreateHost(builder);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && _databaseCreated)
        {
            using var database = NewContext();
            database.Database.EnsureDeleted();
            _databaseCreated = false;
        }
    }

    private ApexPostDbContext NewContext() =>
        new(new DbContextOptionsBuilder<ApexPostDbContext>().UseSqlServer(ConnectionString).Options);

    private static string ServerConnection()
    {
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets(typeof(ApexRoutes).Assembly, optional: true)
            .AddEnvironmentVariables()
            .Build();
        return configuration.GetConnectionString(TestServerName)
            ?? configuration.GetConnectionString(ApplicationDatabaseName)
            ?? throw new InvalidOperationException(
                $"Set ConnectionStrings:{TestServerName}, or run setup-secrets.bat, to reach a SQL Server.");
    }
}
