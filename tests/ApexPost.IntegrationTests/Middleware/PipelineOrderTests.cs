using System.Net;
using ApexPost.IntegrationTests.Support;
using ApexPost.Web.Middleware;
using ApexPost.Web.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ApexPost.IntegrationTests.Middleware;

/// <summary>Each test shows which of two neighbouring steps answers first.</summary>
public sealed class PipelineOrderTests(ApexFactory factory) : IClassFixture<ApexFactory>
{
    [Fact]
    public async Task Ip_restriction_answers_before_the_api_key()
    {
        using var client = PartnerClient.Create(factory, apiKey: null, address: PartnerClient.BlockedAddress);

        using var response = await GetParcelAsync(client);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Cors_preflight_answers_before_the_api_key()
    {
        using var client = PartnerClient.Create(factory, apiKey: null);
        using var request = new HttpRequestMessage(HttpMethod.Options, new Uri(PartnerClient.Parcel, UriKind.Relative));
        request.Headers.Add("Origin", ApexFactory.PartnerOrigin);
        request.Headers.Add("Access-Control-Request-Method", "GET");
        request.Headers.Add("Access-Control-Request-Headers", ApexHeaders.ApiKey);

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Maintenance_answers_before_endpoint_security_and_static_files()
    {
        using var maintenance = new ApexFactory();
        using var client = PartnerClient.Create(maintenance, apiKey: null, address: PartnerClient.BlockedAddress);
        SetMaintenance(maintenance, enabled: true);

        using var parcel = await GetParcelAsync(client);
        using var css = await client.GetAsync(
            new Uri("/css/site.css", UriKind.Relative),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, parcel.StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, css.StatusCode);
    }

    [Fact]
    public async Task Api_key_answers_before_the_rate_limiter()
    {
        using var unknown = PartnerClient.Create(factory, apiKey: "not-a-key");

        for (var attempt = 0; attempt < 40; attempt++)
        {
            using var response = await GetParcelAsync(unknown);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }

    [Fact]
    public async Task Admin_allowlist_is_read_from_its_own_setting()
    {
        var settings = Options.Create(new SecuritySettings
        {
            AdminAllowedIps = ["192.0.2.1"],
            IntegrationAllowedIps = ["192.0.2.2"],
        });
        var calls = 0;
        var middleware = new IpRestrictionMiddleware(
            _ =>
            {
                calls++;
                return Task.CompletedTask;
            },
            settings,
            NullLogger<IpRestrictionMiddleware>.Instance);

        var allowed = AdminCall("192.0.2.1");
        var mapped = AdminCall("::ffff:192.0.2.1");
        var integrationOnly = AdminCall("192.0.2.2");
        await middleware.InvokeAsync(allowed);
        await middleware.InvokeAsync(mapped);
        await middleware.InvokeAsync(integrationOnly);

        Assert.Equal(2, calls);
        Assert.Equal(StatusCodes.Status403Forbidden, integrationOnly.Response.StatusCode);
    }

    [Fact]
    public async Task Unknown_address_is_blocked()
    {
        var settings = Options.Create(new SecuritySettings { AdminAllowedIps = ["192.0.2.1"] });
        var middleware = new IpRestrictionMiddleware(
            _ => Task.CompletedTask,
            settings,
            NullLogger<IpRestrictionMiddleware>.Instance);
        var call = AdminCall(address: null);

        await middleware.InvokeAsync(call);

        Assert.Equal(StatusCodes.Status403Forbidden, call.Response.StatusCode);
    }

    internal static void SetMaintenance(ApexFactory target, bool enabled)
    {
        var configuration = (IConfigurationRoot)target.Services.GetRequiredService<IConfiguration>();
        configuration["Maintenance:Enabled"] = enabled.ToString();
        configuration.Reload();
    }

    private static Task<HttpResponseMessage> GetParcelAsync(HttpClient client) =>
        client.GetAsync(new Uri(PartnerClient.Parcel, UriKind.Relative), TestContext.Current.CancellationToken);

    private static DefaultHttpContext AdminCall(string? address)
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Accept = "text/html";
        context.Connection.RemoteIpAddress = address is null ? null : IPAddress.Parse(address);
        context.SetEndpoint(new Endpoint(
            _ => Task.CompletedTask,
            new EndpointMetadataCollection(new IpRestrictedAttribute(IpAllowlist.Admin)),
            "admin"));
        return context;
    }
}
