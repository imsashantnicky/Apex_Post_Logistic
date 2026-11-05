using ApexPost.Domain.Common;

namespace ApexPost.Domain.Tracking;

/// <summary>
/// One scan uploaded from a courier device. Scans are append-only history; <see cref="ScanId"/> is the id the
/// device gave the scan (<c>scan::{Guid}</c>) and makes uploading the same scan twice harmless.
/// </summary>
public sealed class TrackingScan : Entity<Guid>
{
    public const int LocationLength = 100;
    public const int NotesLength = 500;

    private TrackingScan(Guid id)
        : base(id)
    {
    }

    // Only for persistence frameworks.
    private TrackingScan()
    {
    }

    public string ScanId { get; private set; } = string.Empty;

    public Guid ShipmentId { get; private set; }

    public ScanType ScanType { get; private set; }

    public string CourierId { get; private set; } = string.Empty;

    public string Location { get; private set; } = string.Empty;

    public DateTime ScannedAtUtc { get; private set; }

    public string? Notes { get; private set; }

    /// <summary>When the server stored the scan.</summary>
    public DateTime ReceivedAtUtc { get; private set; }

    /// <summary>Records a scan of <paramref name="shipment"/>; every field is checked here.</summary>
    public static TrackingScan Record(TrackedShipment shipment, ScanDetails details, DateTime receivedAtUtc)
    {
        Guard.Required(shipment, "Shipment");
        Guard.Required(details, "Scan");
        if (!TrackingFormats.IsScanId(details.ScanId))
        {
            throw new DomainException("Scan id must be scan:: followed by a GUID.");
        }

        if (!TrackingFormats.IsCourierId(details.CourierId))
        {
            throw new DomainException("Courier id must be CR- followed by 3 digits.");
        }

        return new TrackingScan(Guid.CreateVersion7())
        {
            ScanId = details.ScanId,
            ShipmentId = shipment.Id,
            ScanType = Guard.Defined(details.ScanType, "Scan type"),
            CourierId = details.CourierId,
            Location = Guard.NotBlank(details.Location, "Location", LocationLength),
            ScannedAtUtc = TrackedShipment.Utc(details.ScannedAtUtc),
            Notes = string.IsNullOrWhiteSpace(details.Notes)
                ? null
                : Guard.NotBlank(details.Notes, "Notes", NotesLength),
            ReceivedAtUtc = TrackedShipment.Utc(receivedAtUtc),
        };
    }
}
