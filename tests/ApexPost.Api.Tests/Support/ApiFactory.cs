using System.Net.Http.Headers;
using ApexPost.Infrastructure.Tracking;
using ApexPost.Tests.Common;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ApexPost.Api.Tests.Support;

/// <summary>
/// The real API host on a throw-away SQL Server database of its own, plus <see cref="ThrowingController"/>. The
/// server comes from <see cref="SqlTestServer"/> (CI variable, user-secrets, LocalDB, else the test is skipped); the
/// development database itself is never touched. In Development the host migrates and seeds the database by itself;
/// the database is dropped with the factory.
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>
{
    private bool _hostCreated;

    public ApiFactory()
    {
        ConnectionString = SqlTestServer.NewDatabase("Api");
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
        if (_hostCreated)
        {
            await using var database = NewContext();
            await database.Database.EnsureDeletedAsync();
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Skips the test when no SQL Server was found; otherwise starts the host, which migrates in Development.
    /// </summary>
    protected override IHost CreateHost(IHostBuilder builder)
    {
        SqlTestServer.SkipIfUnavailable();
        _hostCreated = true;
        return base.CreateHost(builder);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(EnvironmentName);
        builder.ConfigureAppConfiguration((_, configuration) => configuration
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"ConnectionStrings:{TrackingDependencyInjection.ConnectionName}"] = ConnectionString,
            })
            .AddInMemoryCollection(TestTokens.HostConfiguration)
            .AddInMemoryCollection(TestPartnerKeys.HostConfiguration));
        builder.ConfigureTestServices(services =>
            services.AddControllers().AddApplicationPart(typeof(ThrowingController).Assembly));
    }

    /// <summary>
    /// Every client sends a token with both roles for CR-101, so the endpoint tests are about the endpoints; the
    /// security tests replace or remove it (<see cref="TestTokens.ClientWith"/>).
    /// </summary>
    protected override void ConfigureClient(HttpClient client)
    {
        ArgumentNullException.ThrowIfNull(client);
        base.ConfigureClient(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestTokens.AllRoles());
    }

    private TrackingDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<TrackingDbContext>();
        TrackingDependencyInjection.UseTrackingSqlServer(options, ConnectionString);
        return new TrackingDbContext(options.Options);
    }
}
