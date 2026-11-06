using System.Text.Json;
using ApexPost.Api.OpenApi;
using ApexPost.Api.Tests.Support;

namespace ApexPost.Api.Tests.Documentation;

/// <summary>Both OpenAPI documents declare and require the JWT bearer scheme (Swagger UI "Authorize").</summary>
public sealed class SecuritySchemeTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("v1")]
    [InlineData("v2")]
    public async Task Document_declares_and_requires_the_bearer_scheme(string version)
    {
        using var client = factory.CreateClient();

        using var document = JsonDocument.Parse(await client.GetStringAsync($"/openapi/{version}.json", Ct));

        var root = document.RootElement;
        var scheme = root.GetProperty("components").GetProperty("securitySchemes")
            .GetProperty(BearerSecuritySchemeTransformer.SchemeName);
        Assert.Equal("http", scheme.GetProperty("type").GetString());
        Assert.Equal("bearer", scheme.GetProperty("scheme").GetString());
        Assert.Equal("JWT", scheme.GetProperty("bearerFormat").GetString());
        var requirement = Assert.Single(root.GetProperty("security").EnumerateArray());
        Assert.True(requirement.TryGetProperty(BearerSecuritySchemeTransformer.SchemeName, out _));
    }
}
