using ApexPost.Web.Middleware;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ApexPost.IntegrationTests.Support;

/// <summary>A courier partner's HTTP client, calling from an address on the integration allowlist by default.</summary>
internal static class PartnerClient
{
    public const string Parcel = "/integration/parcel-status/APX123456";
    public const string AllowedAddress = "127.0.0.1";
    public const string BlockedAddress = "203.0.113.9";

    public static HttpClient Create(
        ApexFactory factory,
        string? apiKey = ApexFactory.PartnerKey,
        string address = AllowedAddress)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
        });
        client.DefaultRequestHeaders.Add(RemoteAddressStartupFilter.Header, address);
        if (apiKey is not null)
        {
            client.DefaultRequestHeaders.Add(ApexHeaders.ApiKey, apiKey);
        }

        return client;
    }
}
