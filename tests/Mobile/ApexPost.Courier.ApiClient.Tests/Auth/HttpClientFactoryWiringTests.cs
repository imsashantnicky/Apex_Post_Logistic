using System.Net;
using ApexPost.Courier.ApiClient.Auth;
using ApexPost.Courier.ApiClient.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace ApexPost.Courier.ApiClient.Tests.Auth;

/// <summary>The same registration as <c>MauiProgram</c>, with the fake handler as the primary handler.</summary>
[Trait("Category", "Unit")]
public sealed class HttpClientFactoryWiringTests
{
    [Fact]
    public async Task AddHttpClient_TypedClientFromFactory_HasBaseAddressAndAuthHandler()
    {
        // Arrange
        var inner = FakeHttpMessageHandler.Returning(HttpStatusCode.OK, SampleJson.Shipment, SampleJson.Json);
        var services = new ServiceCollection();
        services.AddSingleton<IAccessTokenStore>(new InMemoryTokenStore("factory-token"));
        services.AddTransient<AuthHeaderHandler>();
        services.AddHttpClient<ApexApiClient>(client => client.BaseAddress = ClientFactory.BaseAddress)
            .AddHttpMessageHandler<AuthHeaderHandler>()
            .ConfigurePrimaryHttpMessageHandler(() => new NetworkGuardHandler(inner));
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true });
        var client = provider.GetRequiredService<ApexApiClient>();

        // Act
        var result = await client.GetShipmentAsync("APX-20000005", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsSuccess);
        var sent = Assert.Single(inner.Requests);
        Assert.Equal("Bearer factory-token", sent.Authorization);
        Assert.Equal("https://apexpost.test/api/v2/shipments/APX-20000005", sent.Uri.AbsoluteUri);
    }
}
