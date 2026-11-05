using ApexPost.Infrastructure.Tracking;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ApexPost.Api.Tests.Support;

/// <summary>
/// The real API host on a throw-away SQL Server database of its own, plus <see cref="ThrowingController"/>. The
/// server comes from ConnectionStrings:ApexPostTestServer (the CI container), or else from the user-secrets shared
/// with the web project; the development database itself is never touched. In Development the host migrates and
/// seeds the database by itself; the database is dropped with the factory.
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>
{
    private const string TestServerName = "ApexPostTestServer";
    private const string WebUserSecretsId = "8ee05317-5b65-4866-a83b-e9e701628c18";

    public ApiFactory()
    {
        ConnectionString = new SqlConnectionStringBuilder(ServerConnection())
        {
            InitialCatalog = "ApexPostTests_Api_" + Guid.NewGuid().ToString("N"),
        }.ConnectionString;
    }

    public string ConnectionString { get; }

    protected virtual string EnvironmentName => Environments.Development;

    /// <summary>A tracking context on the test database, for checks behind the API's back; starts the host first.</summary>
    public TrackingDbContext OpenTrackingDatabase()
    {
        _ = Services;
        return NewContext();
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await using var database = NewContext();
        await database.Database.EnsureDeletedAsync();
        GC.SuppressFinalize(this);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(EnvironmentName);
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                [$"ConnectionStrings:{TrackingDependencyInjection.ConnectionName}"] = ConnectionString,
            }));
        builder.ConfigureTestServices(services =>
            services.AddControllers().AddApplicationPart(typeof(ThrowingController).Assembly));
    }

    private TrackingDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<TrackingDbContext>();
        TrackingDependencyInjection.UseTrackingSqlServer(options, ConnectionString);
        return new TrackingDbContext(options.Options);
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
