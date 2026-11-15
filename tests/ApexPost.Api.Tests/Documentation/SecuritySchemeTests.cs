using System.Text.Json;
using ApexPost.Api.Controllers;
using ApexPost.Api.OpenApi;
using ApexPost.Api.Security;
using ApexPost.Api.Tests.Support;

namespace ApexPost.Api.Tests.Documentation;

/// <summary>Both OpenAPI documents declare and require the JWT bearer scheme (Swagger UI "Authorize").</summary>
public sealed class SecuritySchemeTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string PartnerPath = "/api/v2/partner/shipments/{trackingNumber}";

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
            .GetProperty(SecuritySchemeTransformer.BearerScheme);
        Assert.Equal("http", scheme.GetProperty("type").GetString());
        Assert.Equal("bearer", scheme.GetProperty("scheme").GetString());
        Assert.Equal("JWT", scheme.GetProperty("bearerFormat").GetString());
        var requirement = Assert.Single(root.GetProperty("security").EnumerateArray());
        Assert.True(requirement.TryGetProperty(SecuritySchemeTransformer.BearerScheme, out _));
    }

    [Fact]
    public async Task V2_declares_the_ApiKey_scheme_and_the_partner_operation_requires_only_it()
    {
        using var client = factory.CreateClient();

        using var document = JsonDocument.Parse(await client.GetStringAsync("/openapi/v2.json", Ct));

        var root = document.RootElement;
        var scheme = root.GetProperty("components").GetProperty("securitySchemes")
            .GetProperty(SecuritySchemeTransformer.ApiKeyScheme);
        Assert.Equal("apiKey", scheme.GetProperty("type").GetString());
        Assert.Equal("header", scheme.GetProperty("in").GetString());
        Assert.Equal(PartnerApiKeyFilter.HeaderName, scheme.GetProperty("name").GetString());
        var partner = root.GetProperty("paths").GetProperty(PartnerPath).GetProperty("get");
        Assert.Equal(ApiRoutes.GetPartnerShipment, partner.GetProperty("operationId").GetString());
        var requirement = Assert.Single(partner.GetProperty("security").EnumerateArray());
        Assert.Equal(
            [SecuritySchemeTransformer.ApiKeyScheme],
            requirement.EnumerateObject().Select(property => property.Name));
        var shipment = root.GetProperty("paths").GetProperty("/api/v2/shipments/{trackingNumber}").GetProperty("get");
        Assert.False(shipment.TryGetProperty("security", out _));
    }

    [Fact]
    public async Task V1_has_no_partner_operation_and_no_ApiKey_scheme()
    {
        using var client = factory.CreateClient();

        using var document = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json", Ct));

        var root = document.RootElement;
        var schemes = root.GetProperty("components").GetProperty("securitySchemes");
        Assert.False(schemes.TryGetProperty(SecuritySchemeTransformer.ApiKeyScheme, out _));
        Assert.DoesNotContain(
            root.GetProperty("paths").EnumerateObject(),
            path => path.Name.Contains("/partner/", StringComparison.Ordinal));
    }
}
