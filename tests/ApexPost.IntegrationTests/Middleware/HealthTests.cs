using System.Net;
using ApexPost.Application.Abstractions;
using ApexPost.IntegrationTests.Support;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace ApexPost.IntegrationTests.Middleware;

public sealed class HealthTests(ProductionFactory factory) : IClassFixture<ProductionFactory>
{
    [Fact]
    public async Task Health_is_anonymous_and_healthy_when_data_can_be_written()
    {
        using var client = PartnerClient.Create(factory, apiKey: null, address: PartnerClient.BlockedAddress);

        using var response = await client.GetAsync(HealthPath, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Health_answers_503_when_data_cannot_be_written()
    {
        using var broken = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddSingleton<IDataStoreHealth>(new ReadOnlyDataStore())));
        using var client = broken.CreateClient();

        using var response = await client.GetAsync(HealthPath, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    private static Uri HealthPath => new("/health", UriKind.Relative);

    private sealed class ReadOnlyDataStore : IDataStoreHealth
    {
        public Task<bool> CanWriteAsync(CancellationToken cancellationToken) => Task.FromResult(false);
    }
}
