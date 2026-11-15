using System.Net.Http.Headers;
using ApexPost.Infrastructure.Tracking;
using ApexPost.Tests.Common;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Time.Testing;
using Microsoft.IdentityModel.Tokens;

namespace ApexPost.Api.Tests.Support;

/// <summary>
/// The real API host on a throw-away SQL Server database of its own, plus <see cref="ThrowingController"/>. The
/// server comes from <see cref="SqlTestServer"/> (CI variable, user-secrets, LocalDB, else the test is skipped); the
/// development database itself is never touched. In Development the host migrates and seeds the database by itself;
/// the database is dropped with the factory.
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>
{
    /// <summary>
    /// The fictional instant the host clock starts at. Scan times and token lifetimes in the tests are computed from
    /// it, never from the wall clock.
    /// </summary>
    public static readonly DateTimeOffset FixedNow = FixedTimeProvider.DefaultNow;

    private bool _hostCreated;

    public ApiFactory()
    {
        ConnectionString = SqlTestServer.NewDatabase("Api");
    }

    public string ConnectionString { get; }

    /// <summary>
    /// The host's clock: starts at <see cref="FixedNow"/> and moves 1 ms on every read (distinct, ordered times). The
    /// services, the [NotInFuture] rule, the dev-token issuer and the real JWT lifetime check all read it.
    /// </summary>
    public FakeTimeProvider Clock { get; } = new(FixedNow) { AutoAdvanceAmount = TimeSpan.FromMilliseconds(1) };

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
        {
            services.AddControllers().AddApplicationPart(typeof(ThrowingController).Assembly);
            services.Replace(ServiceDescriptor.Singleton<TimeProvider>(Clock));

            // IdentityModel reads the wall clock for the lifetime check and has no public clock to inject, so only
            // that check is replaced by the same rule on the host clock; signature, issuer and audience stay real.
            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
                options.TokenValidationParameters.LifetimeValidator = ValidateLifetime);
        });
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

    /// <summary>
    /// The IdentityModel lifetime rule (expiry required, ClockSkew on both sides) against <see cref="Clock"/>. It
    /// throws the same exception types, so the 401 details of the real pipeline stay the same.
    /// </summary>
    private bool ValidateLifetime(
        DateTime? notBefore,
        DateTime? expires,
        SecurityToken token,
        TokenValidationParameters parameters)
    {
        var now = Clock.GetUtcNow().UtcDateTime;
        if (expires is null)
        {
            throw new SecurityTokenNoExpirationException("The token has no expiry.");
        }

        if (notBefore > now + parameters.ClockSkew)
        {
            throw new SecurityTokenNotYetValidException("The token is not valid yet.") { NotBefore = notBefore.Value };
        }

        if (expires < now - parameters.ClockSkew)
        {
            throw new SecurityTokenExpiredException("The token has expired.") { Expires = expires.Value };
        }

        return true;
    }

    private TrackingDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<TrackingDbContext>();
        TrackingDependencyInjection.UseTrackingSqlServer(options, ConnectionString);
        return new TrackingDbContext(options.Options);
    }
}
