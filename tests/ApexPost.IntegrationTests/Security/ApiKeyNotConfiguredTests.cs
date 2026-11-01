using System.Net;
using ApexPost.IntegrationTests.Support;

namespace ApexPost.IntegrationTests.Security;

public sealed class ApiKeyNotConfiguredTests(NoApiKeysFactory factory) : IClassFixture<NoApiKeysFactory>
{
    [Theory]
    [InlineData(null)]
    [InlineData("any-key")]
    public async Task Integration_returns_500_when_no_key_is_configured(string? key)
    {
        using var client = PartnerClient.Create(factory, key);

        using var response = await client.GetAsync(
            new Uri(PartnerClient.Parcel, UriKind.Relative),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }
}
