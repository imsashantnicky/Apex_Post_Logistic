using System.Net;
using System.Text;
using ApexPost.Api.Errors;
using ApexPost.Api.Tests.Support;

namespace ApexPost.Api.Tests.Endpoints;

/// <summary>
/// When the service fails unexpectedly, every real endpoint answers 500 ProblemDetails from the global handler with
/// a generic detail and a trace id, and never shows the exception message.
/// </summary>
public sealed class ServerErrorEndpointTests(FailingServiceApiFactory factory) : IClassFixture<FailingServiceApiFactory>
{
    private const string Shipment = "/api/shipments/APX-20000001";
    private const string Recipient =
        "\"recipientName\":\"Ava Example\",\"recipientPhone\":\"+44 20 7946 0101\"," +
        "\"recipientAddress\":\"12 Quay Street\",\"recipientCity\":\"Harbour Point\",\"weightKg\":2.5";
    private const string CreateBody = "{\"trackingNumber\":\"APX-30000001\"," + Recipient + "}";
    private const string UpdateBody = "{" + Recipient + "}";
    private const string ScanBody =
        "{\"scans\":[{\"scanId\":\"scan::0f8fad5b-d9cb-469f-a165-70867728950e\"," +
        "\"trackingNumber\":\"APX-20000001\",\"scanType\":1,\"courierId\":\"CR-101\"," +
        "\"location\":\"Central Hub\",\"scannedAtUtc\":\"2020-01-01T00:00:00Z\"}]}";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static TheoryData<string, string, string?> Endpoints => new()
    {
        { "GET", "/api/shipments", null },
        { "GET", Shipment, null },
        { "POST", "/api/shipments", CreateBody },
        { "PUT", Shipment, UpdateBody },
        { "PATCH", Shipment + "/status", "{\"status\":2}" },
        { "DELETE", Shipment, null },
        { "POST", Shipment + "/scans", ScanBody },
        { "GET", Shipment + "/scans", null },
    };

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task A_failing_service_gives_500_ProblemDetails_without_the_message(
        string method,
        string url,
        string? json)
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), url);
        if (json is not null)
        {
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }

        var response = await client.SendAsync(request, Ct);
        var body = await response.Content.ReadAsStringAsync(Ct);
        var problem = await Problem.ReadAsync(response, HttpStatusCode.InternalServerError);

        Assert.Equal(ApiExceptionHandler.UnexpectedDetail, problem.Detail);
        Assert.False(problem.Has(ApiExceptionHandler.StackTraceKey));
        Assert.False(string.IsNullOrWhiteSpace(problem.TraceId));
        Assert.DoesNotContain(FailingTrackingService.SecretMessage, body, StringComparison.Ordinal);
    }
}
