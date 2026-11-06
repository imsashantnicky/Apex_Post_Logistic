using System.Net;
using System.Text.Json;
using ApexPost.Api.OpenApi;
using ApexPost.Api.Tests.Support;

namespace ApexPost.Api.Tests.Documentation;

/// <summary>In Development the built-in OpenAPI document and Swagger UI are served.</summary>
public sealed class DevelopmentDocumentationTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task OpenApi_document_is_served_as_json_with_the_info_of_the_transformer()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync(ApiDocumentation.V1DocumentUrl, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        var info = document.RootElement.GetProperty("info");
        Assert.Equal(ApexDocumentTransformer.Title, info.GetProperty("title").GetString());
        Assert.Equal(ApexDocumentTransformer.Description, info.GetProperty("description").GetString());
        Assert.Equal(ApiDocumentation.V1, info.GetProperty("version").GetString());
        var contact = info.GetProperty("contact");
        Assert.Equal(ApexDocumentTransformer.ContactName, contact.GetProperty("name").GetString());
        Assert.Equal(ApexDocumentTransformer.ContactEmail, contact.GetProperty("email").GetString());
    }

    [Fact]
    public async Task OpenApi_document_describes_the_shipment_endpoints()
    {
        using var client = factory.CreateClient();

        using var document = JsonDocument.Parse(await client.GetStringAsync(ApiDocumentation.V1DocumentUrl, Ct));
        var paths = document.RootElement.GetProperty("paths");

        Assert.True(paths.TryGetProperty("/api/v1/shipments", out _));
        Assert.True(paths.TryGetProperty("/api/v1/shipments/{trackingNumber}", out _));
        Assert.True(paths.TryGetProperty("/api/v1/shipments/{trackingNumber}/scans", out _));
    }

    [Fact]
    public async Task Swagger_ui_page_is_served_and_reads_the_built_in_document()
    {
        using var client = factory.CreateClient();

        var page = await client.GetAsync("/swagger/index.html", Ct);
        var initializer = await client.GetStringAsync("/swagger/index.js", Ct);

        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Equal("text/html", page.Content.Headers.ContentType?.MediaType);
        Assert.Contains(ApiDocumentation.V1DocumentUrl, initializer, StringComparison.Ordinal);
        Assert.Contains(ApiDocumentation.V2DocumentUrl, initializer, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Swagger_root_redirects_to_the_ui_page()
    {
        using var client = factory.CreateClient(new() { AllowAutoRedirect = false });

        var response = await client.GetAsync("/swagger", Ct);

        Assert.Equal(HttpStatusCode.MovedPermanently, response.StatusCode);
        Assert.EndsWith("swagger/index.html", response.Headers.Location?.OriginalString, StringComparison.Ordinal);
    }
}
