using System.Net;
using ApexPost.Courier.ApiClient.Results;
using ApexPost.Courier.ApiClient.Tests.Support;

namespace ApexPost.Courier.ApiClient.Tests.Client;

/// <summary>Bad success bodies never crash the app: each becomes an <see cref="ApiFailureKind.InvalidResponse"/>.</summary>
public sealed class ResponseRobustnessTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static TheoryData<string, string> UnreadableBodies => new()
    {
        { "malformed JSON", """{ "trackingNumber": "APX-20000005", """ },
        { "empty body", string.Empty },
        { "JSON null", "null" },
        { "array instead of object", "[]" },
        { "HTML page", "<html><body>Proxy error</body></html>" },
        { "null in a required string", SampleJson.Shipment.Replace("\"APX-20000005\"", "null", StringComparison.Ordinal) },
        { "null recipient", SampleJson.ShipmentWithoutScan.Replace("\"recipient\": {", "\"recipient\": null, \"x\": {", StringComparison.Ordinal) },
        { "missing required member", """{ "trackingNumber": "APX-20000005" }""" },
        { "unknown status name", SampleJson.Shipment.Replace("\"status\": \"InTransit\"", "\"status\": \"Lost\"", StringComparison.Ordinal) },
        { "string where a number belongs", SampleJson.Shipment.Replace("\"version\": 3", "\"version\": \"three\"", StringComparison.Ordinal) },
    };

    [Theory]
    [MemberData(nameof(UnreadableBodies))]
    public async Task An_unreadable_success_body_becomes_an_invalid_response_failure(string caseName, string body)
    {
        var handler = FakeHttpMessageHandler.Returning(HttpStatusCode.OK, body, SampleJson.Json);

        var result = await ClientFactory.Create(handler).GetShipmentAsync("APX-20000005", Ct);

        Assert.False(result.IsSuccess, caseName);
        Assert.Equal(ApiFailureKind.InvalidResponse, result.Failure.Kind);
        Assert.Equal(FailureMessages.InvalidResponse, result.Failure.Message);
        Assert.Equal(200, result.Failure.StatusCode);
    }

    [Fact]
    public async Task Unknown_members_are_ignored()
    {
        var body = SampleJson.Shipment.Replace(
            "\"version\": 3",
            "\"version\": 3, \"futureField\": { \"nested\": [1, 2] }, \"eta\": null",
            StringComparison.Ordinal);
        var handler = FakeHttpMessageHandler.Returning(HttpStatusCode.OK, body, SampleJson.Json);

        var result = await ClientFactory.Create(handler).GetShipmentAsync("APX-20000005", Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value.Version);
    }

    [Fact]
    public async Task Property_names_are_read_case_insensitively()
    {
        var body = SampleJson.Shipment
            .Replace("\"trackingNumber\"", "\"TrackingNumber\"", StringComparison.Ordinal)
            .Replace("\"status\"", "\"STATUS\"", StringComparison.Ordinal);
        var handler = FakeHttpMessageHandler.Returning(HttpStatusCode.OK, body, SampleJson.Json);

        var result = await ClientFactory.Create(handler).GetShipmentAsync("APX-20000005", Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal("APX-20000005", result.Value.TrackingNumber);
    }

    [Fact]
    public async Task A_null_rejected_list_in_an_upload_answer_is_an_invalid_response()
    {
        const string answer = """{ "accepted": 1, "duplicatesSkipped": 0, "rejected": null }""";
        var handler = FakeHttpMessageHandler.Returning(HttpStatusCode.OK, answer, SampleJson.Json);

        var result = await ClientFactory.Create(handler)
            .UploadScansAsync("APX-20000005", new Contracts.ScanUploadRequest([]), Ct);

        Assert.Equal(ApiFailureKind.InvalidResponse, result.Failure?.Kind);
    }
}
