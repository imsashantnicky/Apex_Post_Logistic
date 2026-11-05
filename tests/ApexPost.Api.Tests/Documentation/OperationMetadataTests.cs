using System.Text.Json;
using ApexPost.Api.Controllers;
using ApexPost.Api.OpenApi;
using ApexPost.Api.Tests.Support;

namespace ApexPost.Api.Tests.Documentation;

/// <summary>
/// Reads /openapi/v1.json and checks the metadata of every API operation: a stable operationId (the route name),
/// summary, description, tag and exactly the documented response codes, errors as application/problem+json.
/// </summary>
public sealed class OperationMetadataTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string ProblemJson = "application/problem+json";
    private const string ApiPrefix = "/api/";
    private const string ShipmentPath = "/api/shipments/{trackingNumber}";
    private const string ScansPath = ShipmentPath + "/scans";

    private static readonly ExpectedOperation[] Expected =
    [
        new(ApiRoutes.ListShipments, "get", "/api/shipments", ApiRoutes.ShipmentsTag, "List shipments",
            ["200", "400", "500"]),
        new(ApiRoutes.GetShipment, "get", ShipmentPath, ApiRoutes.ShipmentsTag, "Get a shipment",
            ["200", "404", "500"]),
        new(ApiRoutes.CreateShipment, "post", "/api/shipments", ApiRoutes.ShipmentsTag, "Create a shipment",
            ["201", "400", "409", "415", "500"]),
        new(ApiRoutes.ReplaceShipment, "put", ShipmentPath, ApiRoutes.ShipmentsTag, "Replace a shipment",
            ["200", "400", "404", "409", "415", "500"]),
        new(ApiRoutes.ChangeShipmentStatus, "patch", ShipmentPath + "/status", ApiRoutes.ShipmentsTag,
            "Change the status of a shipment", ["200", "400", "404", "409", "415", "500"]),
        new(ApiRoutes.DeleteShipment, "delete", ShipmentPath, ApiRoutes.ShipmentsTag, "Delete a shipment",
            ["204", "404", "409", "500"]),
        new(ApiRoutes.UploadScans, "post", ScansPath, ApiRoutes.ScansTag, "Upload scans of a shipment",
            ["200", "400", "404", "415", "500"]),
        new(ApiRoutes.GetScans, "get", ScansPath, ApiRoutes.ScansTag, "Get the scans of a shipment",
            ["200", "404", "500"]),
    ];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static TheoryData<string> OperationIds => [.. Expected.Select(operation => operation.OperationId)];

    [Fact]
    public async Task Every_api_operation_is_one_of_the_documented_operations()
    {
        using var document = await ReadDocumentAsync();

        var found = ApiOperations(document.RootElement)
            .Select(operation => (operation.Method, operation.Path, Id: OperationId(operation.Body)))
            .OrderBy(operation => operation.Id, StringComparer.Ordinal)
            .ToList();
        var expected = Expected
            .Select(operation => (operation.Method, operation.Path, Id: (string?)operation.OperationId))
            .OrderBy(operation => operation.Id, StringComparer.Ordinal)
            .ToList();

        Assert.Equal(expected, found);
    }

    [Fact]
    public async Task Every_api_operation_has_a_unique_operation_id_summary_description_and_tag()
    {
        using var document = await ReadDocumentAsync();

        var operations = ApiOperations(document.RootElement).ToList();

        Assert.NotEmpty(operations);
        Assert.All(operations, operation =>
        {
            Assert.False(string.IsNullOrWhiteSpace(OperationId(operation.Body)), $"{operation} has no operationId");
            Assert.False(string.IsNullOrWhiteSpace(Text(operation.Body, "summary")), $"{operation} has no summary");
            Assert.False(string.IsNullOrWhiteSpace(Text(operation.Body, "description")), $"{operation} has none");
            Assert.Single(operation.Body.GetProperty("tags").EnumerateArray());
        });
        var ids = operations.Select(operation => OperationId(operation.Body)).ToList();
        Assert.Equal(ids.Count, ids.Distinct(StringComparer.Ordinal).Count());
    }

    [Theory]
    [MemberData(nameof(OperationIds))]
    public async Task Operation_has_its_route_name_summary_tag_and_exactly_its_response_codes(string operationId)
    {
        var expected = Expected.Single(operation => operation.OperationId == operationId);
        using var document = await ReadDocumentAsync();

        var operation = document.RootElement.GetProperty("paths").GetProperty(expected.Path)
            .GetProperty(expected.Method);

        Assert.Equal(operationId, OperationId(operation));
        Assert.Equal(expected.Summary, Text(operation, "summary"));
        Assert.Equal(expected.Tag, Assert.Single(operation.GetProperty("tags").EnumerateArray()).GetString());
        var codes = operation.GetProperty("responses").EnumerateObject().Select(response => response.Name);
        Assert.Equal(expected.ResponseCodes, codes.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task Every_error_response_is_problem_details_and_only_problem_json()
    {
        using var document = await ReadDocumentAsync();

        var errors = ApiOperations(document.RootElement)
            .SelectMany(operation => operation.Body.GetProperty("responses").EnumerateObject())
            .Where(response => response.Name[0] is '4' or '5')
            .ToList();

        Assert.NotEmpty(errors);
        Assert.All(errors, response =>
        {
            var content = Assert.Single(response.Value.GetProperty("content").EnumerateObject());
            Assert.Equal(ProblemJson, content.Name);
            var schema = content.Value.GetProperty("schema").GetProperty("$ref").GetString();
            var expectedSchema = response.Name == "400" ? "ValidationProblemDetails" : "ProblemDetails";
            Assert.Equal($"#/components/schemas/{expectedSchema}", schema);
        });
    }

    [Fact]
    public async Task Delete_answer_204_promises_no_body()
    {
        using var document = await ReadDocumentAsync();

        var noContent = document.RootElement.GetProperty("paths").GetProperty(ShipmentPath).GetProperty("delete")
            .GetProperty("responses").GetProperty("204");

        Assert.False(noContent.TryGetProperty("content", out _));
    }

    [Theory]
    [InlineData("CreateShipmentRequest")]
    [InlineData("UpdateShipmentRequest")]
    [InlineData("UpdateStatusRequest")]
    [InlineData("ScanUploadRequest")]
    [InlineData("ScanItem")]
    [InlineData("ShipmentResponse")]
    [InlineData("ScanResponse")]
    [InlineData("ScanUploadResult")]
    [InlineData("RejectedScanResponse")]
    public async Task Contract_properties_have_a_description_and_an_example_from_the_xml_comments(string schemaName)
    {
        using var document = await ReadDocumentAsync();

        var properties = document.RootElement.GetProperty("components").GetProperty("schemas")
            .GetProperty(schemaName).GetProperty("properties").EnumerateObject().ToList();

        Assert.NotEmpty(properties);
        Assert.All(properties, property =>
        {
            var schema = NonNullBranch(property.Value);
            Assert.False(string.IsNullOrWhiteSpace(Text(schema, "description")), property.Name);
            Assert.True(schema.TryGetProperty("examples", out _), $"{property.Name} has no example");
        });
    }

    // A nullable enum is written as oneOf [ null, the enum ]; its description and example sit on the enum branch.
    private static JsonElement NonNullBranch(JsonElement schema) =>
        schema.TryGetProperty("oneOf", out var branches)
            ? branches.EnumerateArray().Single(branch => Text(branch, "type") != "null")
            : schema;

    [Fact]
    public async Task Request_body_description_comes_from_the_request_parameter()
    {
        using var document = await ReadDocumentAsync();

        var body = document.RootElement.GetProperty("paths").GetProperty("/api/shipments").GetProperty("post")
            .GetProperty("requestBody");

        Assert.Equal("The tracking number, recipient and weight.", Text(body, "description"));
    }

    private async Task<JsonDocument> ReadDocumentAsync()
    {
        using var client = factory.CreateClient();
        return JsonDocument.Parse(await client.GetStringAsync(ApiDocumentation.V1DocumentUrl, Ct));
    }

    // The test host also serves test-only controllers under /test; only the real API routes are checked.
    private static IEnumerable<ApiOperation> ApiOperations(JsonElement root) =>
        from path in root.GetProperty("paths").EnumerateObject()
        where path.Name.StartsWith(ApiPrefix, StringComparison.Ordinal)
        from operation in path.Value.EnumerateObject()
        where operation.Value.ValueKind == JsonValueKind.Object && operation.Value.TryGetProperty("responses", out _)
        select new ApiOperation(operation.Name, path.Name, operation.Value);

    private static string? OperationId(JsonElement operation) => Text(operation, "operationId");

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) ? value.GetString() : null;

    private sealed record ExpectedOperation(
        string OperationId,
        string Method,
        string Path,
        string Tag,
        string Summary,
        string[] ResponseCodes);

    private sealed record ApiOperation(string Method, string Path, JsonElement Body)
    {
        public override string ToString() => $"{Method.ToUpperInvariant()} {Path}";
    }
}
