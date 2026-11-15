using ApexPost.Infrastructure.Tracking;
using ApexPost.Tests.Common;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Time.Testing;

namespace ApexPost.Specs.Support;

/// <summary>
/// The real API host for one scenario: its own throw-away SQL Server database (<see cref="SqlTestServer"/>), a fixed
/// <see cref="FakeTimeProvider"/> clock and <see cref="SpecAuthHandler"/> as the authentication scheme. Everything
/// behind authentication (validation, role rules, services, EF Core, SQL) is the production code. In Development the
/// host migrates and seeds its database by itself; the database is dropped on dispose.
/// </summary>
public sealed class SpecApiFactory : WebApplicationFactory<Program>
{
    /// <summary>The fictional instant the host clock starts at.</summary>
    public static readonly DateTimeOffset FixedNow = new(2026, 11, 5, 12, 0, 0, TimeSpan.Zero);

    private bool _hostCreated;

    /// <summary>A connection string to a new database name; the host creates the database when it starts.</summary>
    public string ConnectionString { get; } = SqlTestServer.NewDatabase("Specs");

    /// <summary>The host clock: starts at <see cref="FixedNow"/> and moves 1 ms on every read.</summary>
    public FakeTimeProvider Clock { get; } = new(FixedNow) { AutoAdvanceAmount = TimeSpan.FromMilliseconds(1) };

    /// <summary>A client whose requests carry the given roles; no roles means an anonymous caller.</summary>
    public HttpClient ClientAs(params string[] roles)
    {
        ArgumentNullException.ThrowIfNull(roles);
        var client = CreateClient();
        if (roles.Length > 0)
        {
            client.DefaultRequestHeaders.Add(SpecAuthHandler.RolesHeader, string.Join(',', roles));
        }

        return client;
    }

    /// <summary>A tracking context on the scenario database, for set-up behind the API's back; starts the host.</summary>
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

    /// <summary>Skips the scenario when no SQL Server was found (fails it in CI), then starts the host.</summary>
    protected override IHost CreateHost(IHostBuilder builder)
    {
        SqlTestServer.SkipIfUnavailable();
        _hostCreated = true;
        return base.CreateHost(builder);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(Environments.Development);
        builder.ConfigureAppConfiguration((_, configuration) => configuration
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"ConnectionStrings:{TrackingDependencyInjection.ConnectionName}"] = ConnectionString,
            }));
        builder.ConfigureTestServices(services =>
        {
            services.Replace(ServiceDescriptor.Singleton<TimeProvider>(Clock));
            services
                .AddAuthentication(options =>
                {
                    options.DefaultScheme = SpecAuthHandler.SchemeName;
                    options.DefaultAuthenticateScheme = SpecAuthHandler.SchemeName;
                    options.DefaultChallengeScheme = SpecAuthHandler.SchemeName;
                    options.DefaultForbidScheme = SpecAuthHandler.SchemeName;
                })
                .AddScheme<AuthenticationSchemeOptions, SpecAuthHandler>(SpecAuthHandler.SchemeName, _ => { });
        });
    }

    private TrackingDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<TrackingDbContext>();
        TrackingDependencyInjection.UseTrackingSqlServer(options, ConnectionString);
        return new TrackingDbContext(options.Options);
    }
}
