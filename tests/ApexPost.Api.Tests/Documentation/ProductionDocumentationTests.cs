using System.Net;
using ApexPost.Api.OpenApi;
using ApexPost.Api.Tests.Support;

namespace ApexPost.Api.Tests.Documentation;

/// <summary>
/// Outside Development neither the OpenAPI document nor Swagger UI is mapped. This keeps the attack surface small;
/// it is not what protects the endpoints.
/// </summary>
public sealed class ProductionDocumentationTests(ProductionApiFactory factory) : IClassFixture<ProductionApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(ApiDocumentation.V1DocumentUrl)]
    [InlineData("/openapi")]
    [InlineData("/swagger")]
    [InlineData("/swagger/index.html")]
    [InlineData("/swagger/index.js")]
    public async Task Documentation_is_not_found_in_Production(string url)
    {
        using var client = factory.CreateClient(new() { AllowAutoRedirect = false });

        var response = await client.GetAsync(url, Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
