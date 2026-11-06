using System.Text.Json;
using ApexPost.Api.Controllers;
using ApexPost.Api.OpenApi;
using ApexPost.Api.Tests.Support;

namespace ApexPost.Api.Tests.Documentation;

/// <summary>
/// One OpenAPI document per version: v1 lists only deprecated /api/v1 operations with V1 operationIds, v2 lists the
/// /api/v2 operations with bare operationIds and string enums. The unversioned alias is in neither document.
/// </summary>
public sealed class VersionedDocumentTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly string[] V2OperationIds =
    [
        ApiRoutes.ListShipments, ApiRoutes.GetShipment, ApiRoutes.CreateShipment, ApiRoutes.ReplaceShipment,
        ApiRoutes.ChangeShipmentStatus, ApiRoutes.DeleteShipment, ApiRoutes.UploadScans, ApiRoutes.GetScans,
    ];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task V2_document_has_bare_operation_ids_on_v2_paths_and_nothing_deprecated()
    {
        using var document = await ReadAsync(ApiDocumentation.V2DocumentUrl);

        var operations = Operations(document.RootElement).ToList();

        Assert.Equal("v2", document.RootElement.GetProperty("info").GetProperty("version").GetString());
        Assert.Equal(
            V2OperationIds.Order(StringComparer.Ordinal),
            operations.Select(operation => operation.Id).Order(StringComparer.Ordinal));
        Assert.All(operations, operation =>
        {
            Assert.StartsWith("/api/v2/shipments", operation.Path, StringComparison.Ordinal);
            Assert.False(operation.Deprecated, operation.Id);
        });
    }

    [Fact]
    public async Task Only_the_v1_description_carries_the_deprecation_notice_with_the_sunset_date()
    {
        using var v1 = await ReadAsync(ApiDocumentation.V1DocumentUrl);
        using var v2 = await ReadAsync(ApiDocumentation.V2DocumentUrl);

        var v1Description = Description(v1.RootElement);

        Assert.StartsWith(ApexDocumentTransformer.Description, v1Description, StringComparison.Ordinal);
        Assert.Contains("**Deprecated:** v1 is deprecated", v1Description, StringComparison.Ordinal);
        Assert.Contains("2027-05-31", v1Description, StringComparison.Ordinal);
        Assert.Equal(ApexDocumentTransformer.Description, Description(v2.RootElement));
    }

    [Theory]
    [InlineData(ApiDocumentation.V1DocumentUrl)]
    [InlineData(ApiDocumentation.V2DocumentUrl)]
    public async Task Document_lists_no_servers_so_the_contract_is_the_same_on_every_machine(string url)
    {
        using var document = await ReadAsync(url);

        Assert.False(
            document.RootElement.TryGetProperty("servers", out var servers) && servers.GetArrayLength() > 0);
    }

    [Fact]
    public async Task Swagger_ui_offers_v2_and_v1_deprecated()
    {
        using var client = factory.CreateClient();

        var initializer = await client.GetStringAsync("/swagger/index.js", Ct);

        Assert.Contains(
            $"\"url\":\"{ApiDocumentation.V2DocumentUrl}\",\"name\":\"v2\"",
            initializer,
            StringComparison.Ordinal);
        Assert.Contains(
            $"\"url\":\"{ApiDocumentation.V1DocumentUrl}\",\"name\":\"v1 (deprecated)\"",
            initializer,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task V1_document_marks_every_operation_deprecated()
    {
        using var document = await ReadAsync(ApiDocumentation.V1DocumentUrl);

        var operations = Operations(document.RootElement).ToList();

        Assert.Equal(8, operations.Count);
        Assert.All(operations, operation =>
        {
            Assert.StartsWith("/api/v1/shipments", operation.Path, StringComparison.Ordinal);
            Assert.EndsWith("V1", operation.Id, StringComparison.Ordinal);
            Assert.True(operation.Deprecated, operation.Id);
        });
    }

    [Theory]
    [InlineData(ApiDocumentation.V1DocumentUrl)]
    [InlineData(ApiDocumentation.V2DocumentUrl)]
    public async Task Unversioned_alias_is_not_documented(string url)
    {
        using var document = await ReadAsync(url);

        var paths = document.RootElement.GetProperty("paths").EnumerateObject().Select(path => path.Name);

        Assert.DoesNotContain(paths, path => path.StartsWith("/api/shipments", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Status_enum_is_an_integer_in_v1_and_named_strings_in_v2()
    {
        using var v1 = await ReadAsync(ApiDocumentation.V1DocumentUrl);
        using var v2 = await ReadAsync(ApiDocumentation.V2DocumentUrl);

        var v1Status = StatusSchema(v1.RootElement);
        var v2Names = StatusSchema(v2.RootElement).GetProperty("enum").EnumerateArray()
            .Select(value => value.GetString());

        Assert.Equal("integer", v1Status.GetProperty("type").GetString());
        Assert.Equal(["Created", "PickedUp", "InTransit", "OutForDelivery", "Delivered", "Failed"], v2Names);
    }

    private static string? Description(JsonElement root) =>
        root.GetProperty("info").GetProperty("description").GetString();

    private static JsonElement StatusSchema(JsonElement root) =>
        root.GetProperty("components").GetProperty("schemas").GetProperty("ShipmentStatus");

    [Fact]
    public async Task V2_shipment_schema_nests_recipient_and_latest_scan()
    {
        using var document = await ReadAsync(ApiDocumentation.V2DocumentUrl);

        var properties = document.RootElement.GetProperty("components").GetProperty("schemas")
            .GetProperty("ShipmentResponse").GetProperty("properties");

        Assert.Equal(
            "#/components/schemas/RecipientResponse",
            properties.GetProperty("recipient").GetProperty("$ref").GetString());
        Assert.Contains(
            properties.GetProperty("latestScan").GetProperty("oneOf").EnumerateArray(),
            branch => branch.TryGetProperty("$ref", out var reference)
                && reference.GetString() == "#/components/schemas/LatestScanResponse");
    }

    private async Task<JsonDocument> ReadAsync(string url)
    {
        using var client = factory.CreateClient();
        return JsonDocument.Parse(await client.GetStringAsync(url, Ct));
    }

    // The test host also serves test-only controllers under /test; only the real API routes are checked.
    private static IEnumerable<Operation> Operations(JsonElement root) =>
        from path in root.GetProperty("paths").EnumerateObject()
        where path.Name.StartsWith("/api/", StringComparison.Ordinal)
        from operation in path.Value.EnumerateObject()
        where operation.Value.ValueKind == JsonValueKind.Object && operation.Value.TryGetProperty("responses", out _)
        select new Operation(
            path.Name,
            operation.Value.GetProperty("operationId").GetString() ?? string.Empty,
            operation.Value.TryGetProperty("deprecated", out var deprecated) && deprecated.GetBoolean());

    private sealed record Operation(string Path, string Id, bool Deprecated);
}
