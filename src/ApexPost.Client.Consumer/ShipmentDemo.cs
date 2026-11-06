using System.Globalization;
using System.Net;
using System.Security.Cryptography;

namespace ApexPost.Client.Consumer;

/// <summary>
/// The consumer scenario, written only against the generated <see cref="IApexPostApiClient"/>: create a shipment,
/// read it back, upload two courier scans and ask for an unknown tracking number. Output goes to a
/// <see cref="TextWriter"/> so the tests can run the same scenario against the in-memory API.
/// </summary>
public sealed class ShipmentDemo(IApexPostApiClient client, TextWriter output, TimeProvider clock)
{
    /// <summary>A tracking number that matches the route pattern but is never issued, so the API answers 404.</summary>
    public const string UnknownTrackingNumber = "APX-00000000";

    /// <summary>The fictional courier who records the demo scans.</summary>
    public const string CourierId = "CR-101";

    /// <summary>The message shown instead of the raw error when a shipment does not exist.</summary>
    public static string NotFoundMessage(string trackingNumber) =>
        $"No shipment with tracking number {trackingNumber} exists. Check the number and try again.";

    /// <summary>Runs all four steps and returns the tracking number of the shipment it created.</summary>
    public async Task<string> RunAsync(CancellationToken cancellationToken)
    {
        var trackingNumber = NewTrackingNumber();

        var created = await client.CreateShipmentAsync(NewShipment(trackingNumber), cancellationToken);
        Write("1. Created", created);

        var fetched = await client.GetShipmentAsync(trackingNumber, cancellationToken);
        Write("2. Fetched", fetched);

        var upload = await client.UploadScansAsync(trackingNumber, TwoScans(trackingNumber), cancellationToken);
        output.WriteLine(
            $"3. Uploaded 2 scans: accepted {upload.Accepted}, duplicates skipped {upload.DuplicatesSkipped}, " +
            $"rejected {upload.Rejected.Count}");
        foreach (var rejected in upload.Rejected)
        {
            output.WriteLine($"   Rejected {rejected.ScanId}: {rejected.Reason}");
        }

        Write("   Now", await client.GetShipmentAsync(trackingNumber, cancellationToken));

        output.WriteLine($"4. Looking up {UnknownTrackingNumber}...");
        output.WriteLine("   " + await DescribeAsync(UnknownTrackingNumber, cancellationToken));
        return trackingNumber;
    }

    /// <summary>
    /// Fetches a shipment and describes it in one line, or returns a friendly message when the API answers 404.
    /// Every non-success status arrives as <see cref="ApiException"/> (for documented error bodies the subclass
    /// <c>ApiException&lt;ProblemDetails&gt;</c>); other statuses are not handled here and keep propagating.
    /// </summary>
    public async Task<string> DescribeAsync(string trackingNumber, CancellationToken cancellationToken)
    {
        try
        {
            var shipment = await client.GetShipmentAsync(trackingNumber, cancellationToken);
            return $"{shipment.TrackingNumber} is {shipment.Status}.";
        }
        catch (ApiException exception) when (exception.StatusCode == (int)HttpStatusCode.NotFound)
        {
            return NotFoundMessage(trackingNumber);
        }
    }

    // APX-3xxxxxxx stays clear of the seeded APX-2000000x shipments, so the demo can run again on the same database.
    private static string NewTrackingNumber() =>
        "APX-" + RandomNumberGenerator.GetInt32(30_000_000, 40_000_000).ToString(CultureInfo.InvariantCulture);

    private static CreateShipmentRequest NewShipment(string trackingNumber) => new()
    {
        TrackingNumber = trackingNumber,
        WeightKg = 2.5,
        Recipient = new RecipientRequest
        {
            Name = "Noah Example",
            Phone = "+44 20 7946 0199",
            Address = "8 Harbour Lane",
            City = "Harbour Point",
        },
    };

    private ScanUploadRequest TwoScans(string trackingNumber)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        return new ScanUploadRequest
        {
            Scans =
            [
                Scan(trackingNumber, ScanType.Pickup, "Harbour Point depot", now.AddMinutes(-30)),
                Scan(trackingNumber, ScanType.InTransit, "Central sorting hub", now.AddMinutes(-5)),
            ],
        };
    }

    // scan::{Guid} is the idempotency key: sending the same ScanItem again is counted as a duplicate.
    private static ScanItem Scan(string trackingNumber, ScanType type, string location, DateTime at) => new()
    {
        ScanId = "scan::" + Guid.NewGuid().ToString("D"),
        TrackingNumber = trackingNumber,
        ScanType = type,
        CourierId = CourierId,
        Location = location,
        ScannedAtUtc = at,
        Notes = "Uploaded by ApexPost.Client.Consumer.",
    };

    private void Write(string label, ShipmentResponse shipment)
    {
        var latest = shipment.LatestScan is { } scan ? $"{scan.ScanType} at {scan.Location}" : "none";
        output.WriteLine(
            $"{label}: {shipment.TrackingNumber} for {shipment.Recipient.Name}, {shipment.Recipient.City}; " +
            $"{shipment.WeightKg.ToString(CultureInfo.InvariantCulture)} kg; status {shipment.Status}; " +
            $"latest scan {latest}; version {shipment.Version}");
    }
}
