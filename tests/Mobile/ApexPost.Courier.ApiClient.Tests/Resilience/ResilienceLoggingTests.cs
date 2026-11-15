using System.Net;
using ApexPost.Courier.ApiClient.Contracts;
using ApexPost.Courier.ApiClient.Tests.Support;
using ApexPost.Courier.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace ApexPost.Courier.ApiClient.Tests.Resilience;

/// <summary>Every retry and every circuit change is logged; no log line holds the token or the body.</summary>
public sealed class ResilienceLoggingTests
{
    private const string TrackingNumber = "APX-20000005";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Each_retry_is_logged_with_attempt_method_path_delay_and_reason()
    {
        await using var client = new ResilientClient(ScriptedHttpHandler.Statuses(
            SampleJson.Shipment,
            HttpStatusCode.ServiceUnavailable,
            HttpStatusCode.BadGateway,
            HttpStatusCode.OK));

        await client.Api.GetShipmentAsync(TrackingNumber, Ct);

        var retries = client.ResilienceLogs.Where(log => log.EventId == 4101).ToList();
        Assert.Equal(2, retries.Count);
        Assert.All(retries, log => Assert.Equal(LogLevel.Warning, log.Level));
        Assert.Matches(@"^Retry 1 of 3 for GET /api/v2/shipments/APX-20000005 in \d+ ms after HTTP 503$", retries[0].Message);
        Assert.Matches(@"^Retry 2 of 3 for GET /api/v2/shipments/APX-20000005 in \d+ ms after HTTP 502$", retries[1].Message);
    }

    [Fact]
    public async Task Opening_and_closing_the_circuit_is_logged()
    {
        await using var client = new ResilientClient(ScriptedHttpHandler.Statuses(
            SampleJson.Shipment,
            HttpStatusCode.ServiceUnavailable,
            HttpStatusCode.ServiceUnavailable,
            HttpStatusCode.ServiceUnavailable,
            HttpStatusCode.ServiceUnavailable,
            HttpStatusCode.OK));
        await client.Api.GetShipmentAsync(TrackingNumber, Ct);
        await Task.Delay(ResilientClient.FastSettings.BreakDuration + TimeSpan.FromMilliseconds(200), Ct);
        await client.Api.GetShipmentAsync(TrackingNumber, Ct);

        var events = client.ResilienceLogs.Where(log => log.EventId > 4101).ToList();

        Assert.Equal([4102, 4103, 4104], events.Select(log => log.EventId));
        Assert.Equal("Circuit opened for 0.5 s after HTTP 503; the app works offline", events[0].Message);
        Assert.Equal(LogLevel.Error, events[0].Level);
    }

    [Fact]
    public async Task No_log_line_contains_the_token_or_the_request_body()
    {
        const string signatureName = "Mia Example Signature";
        await using var client = new ResilientClient(ScriptedHttpHandler.Statuses(
            """{ "accepted": 1, "duplicatesSkipped": 0, "rejected": [] }""",
            HttpStatusCode.ServiceUnavailable));
        var scan = new ScanItem(
            "scan::7c9e6679-7425-40de-944b-e07fc1f90ae7",
            TrackingNumber,
            ScanType.Delivered,
            "CR-101",
            "Northgate",
            new DateTime(2026, 11, 5, 10, 0, 0, DateTimeKind.Utc),
            "left with neighbour",
            signatureName);

        await client.Api.UploadScansAsync(TrackingNumber, new ScanUploadRequest([scan]), Ct);
        await client.Api.UploadScansAsync(TrackingNumber, new ScanUploadRequest([scan]), Ct);

        Assert.NotEmpty(client.ResilienceLogs);
        Assert.All(client.Logs.Entries, log =>
        {
            Assert.DoesNotContain(ResilientClient.Token, log.Message, StringComparison.Ordinal);
            Assert.DoesNotContain(signatureName, log.Message, StringComparison.Ordinal);
            Assert.DoesNotContain(scan.ScanId, log.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("left with neighbour", log.Message, StringComparison.Ordinal);
        });
    }
}
