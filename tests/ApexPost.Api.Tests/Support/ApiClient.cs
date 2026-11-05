using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ApexPost.Api.Contracts.V1;
using ApexPost.Domain.Tracking;

namespace ApexPost.Api.Tests.Support;

/// <summary>Helpers for the endpoint tests: fresh tracking numbers, scans near the real clock, raw bodies.</summary>
public static class ApiClient
{
    public const string Shipments = "/api/shipments";

    /// <summary>The JSON options the API uses by default (camelCase).</summary>
    public static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    /// <summary>A tracking number outside the seeded APX-20000001..10 range, so tests never collide.</summary>
    public static string NewTrackingNumber() =>
        "APX-3" + Random.Shared.Next(0, 10_000_000).ToString("D7", CultureInfo.InvariantCulture);

    public static string NewScanId() => TrackingFormats.ScanIdPrefix + Guid.NewGuid().ToString("D");

    /// <summary>A valid scan; the host uses the real clock, so the time is taken from it.</summary>
    public static ScanItem Scan(string trackingNumber, ScanType type, int minutesAgo = 30, string? scanId = null) =>
        new()
        {
            ScanId = scanId ?? NewScanId(),
            TrackingNumber = trackingNumber,
            ScanType = type,
            CourierId = "CR-101",
            Location = "Central Hub",
            ScannedAtUtc = DateTime.UtcNow.AddMinutes(-minutesAgo),
            RecipientSignatureName = type == ScanType.Delivered ? "Ava Example" : null,
        };

    /// <summary>Creates a shipment through the API and returns its tracking number.</summary>
    public static async Task<string> CreateShipmentAsync(HttpClient client, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(client);
        var trackingNumber = NewTrackingNumber();
        var request = new CreateShipmentRequest
        {
            TrackingNumber = trackingNumber,
            RecipientName = "Ava Example",
            RecipientPhone = "+44 20 7946 0101",
            RecipientAddress = "12 Quay Street",
            RecipientCity = "Harbour Point",
            WeightKg = 2.5m,
        };
        var response = await client.PostAsJsonAsync(Shipments, request, cancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return trackingNumber;
    }

    /// <summary>Moves a shipment to a status through the API.</summary>
    public static async Task MoveAsync(
        HttpClient client,
        string trackingNumber,
        ShipmentStatus status,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(client);
        var response = await client.PatchAsJsonAsync(
            $"{Shipments}/{trackingNumber}/status",
            new { status },
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>A body with the given media type, to provoke 415.</summary>
    public static StringContent Body(string json, string mediaType) => new(json, Encoding.UTF8, mediaType);
}
