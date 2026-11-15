using System.Net;
using ApexPost.Courier.ApiClient.Auth;
using ApexPost.Courier.ApiClient.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace ApexPost.Courier.ApiClient.Tests.Auth;

/// <summary>The same registration as <c>MauiProgram</c>, with the fake handler as the primary handler.</summary>
public sealed class HttpClientFactoryWiringTests
{
    [Fact]
    public async Task The_typed_client_from_the_factory_has_the_base_address_and_the_auth_handler()
    {
        var inner = FakeHttpMessageHandler.Returning(HttpStatusCode.OK, SampleJson.Shipment, SampleJson.Json);
        var services = new ServiceCollection();
        services.AddSingleton<IAccessTokenStore>(new InMemoryTokenStore("factory-token"));
        services.AddTransient<AuthHeaderHandler>();
        services.AddHttpClient<ApexApiClient>(client => client.BaseAddress = ClientFactory.BaseAddress)
            .AddHttpMessageHandler<AuthHeaderHandler>()
            .ConfigurePrimaryHttpMessageHandler(() => inner);
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true });

        var client = provider.GetRequiredService<ApexApiClient>();
        var result = await client.GetShipmentAsync("APX-20000005", TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var sent = Assert.Single(inner.Requests);
        Assert.Equal("Bearer factory-token", sent.Authorization);
        Assert.Equal("https://localhost:7310/api/v2/shipments/APX-20000005", sent.Uri.AbsoluteUri);
    }
}
