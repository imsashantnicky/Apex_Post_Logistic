using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace ApexPost.Api.Tests.Support;

/// <summary>
/// The real API host on its own throw-away SQL Server database (<see cref="ApiFactory"/>), but with
/// <see cref="TestAuthHandler"/> as the default authentication scheme, registered in <c>ConfigureTestServices</c>.
/// Everything after authentication (role rules, the courier ownership check, services, EF Core, SQL) stays real.
/// Clients send no bearer token; <see cref="ClientAs"/> chooses the roles of the caller.
/// </summary>
public sealed class TestAuthApiFactory : ApiFactory
{
    /// <summary>A client whose requests authenticate with these roles; no roles means an anonymous client.</summary>
    public HttpClient ClientAs(params string[] roles)
    {
        ArgumentNullException.ThrowIfNull(roles);
        var client = CreateClient();
        if (roles.Length > 0)
        {
            client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, string.Join(',', roles));
        }

        return client;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services => services
            .AddAuthentication(options =>
            {
                options.DefaultScheme = TestAuthHandler.SchemeName;
                options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                options.DefaultForbidScheme = TestAuthHandler.SchemeName;
            })
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { }));
    }

    /// <summary>No bearer token: the base factory's default token is removed, so only the test scheme decides.</summary>
    protected override void ConfigureClient(HttpClient client)
    {
        ArgumentNullException.ThrowIfNull(client);
        base.ConfigureClient(client);
        client.DefaultRequestHeaders.Authorization = null;
    }
}
