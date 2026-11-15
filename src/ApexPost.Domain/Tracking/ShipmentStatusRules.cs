using ApexPost.Domain.Common;

namespace ApexPost.Domain.Tracking;

/// <summary>
/// The shipment lifecycle Created → PickedUp → InTransit → OutForDelivery → Delivered | Failed. A shipment only moves
/// forward: it may skip a stage (a Delivered scan can follow an InTransit scan, because couriers have no
/// OutForDelivery scan), but it never goes back, never stays and never leaves Delivered or Failed.
/// </summary>
public static class ShipmentStatusRules
{
    /// <summary>True for Delivered and Failed: nothing changes the status any more.</summary>
    public static bool IsFinal(ShipmentStatus status) =>
        status is ShipmentStatus.Delivered or ShipmentStatus.Failed;

    /// <summary>True when a shipment in <paramref name="from"/> may move to <paramref name="to"/>.</summary>
    public static bool CanMove(ShipmentStatus from, ShipmentStatus to) =>
        Enum.IsDefined(from) && Enum.IsDefined(to) && !IsFinal(from) && Stage(to) > Stage(from);

    /// <summary>The status a scan of the given type asks for.</summary>
    public static ShipmentStatus StatusFor(ScanType scanType) => scanType switch
    {
        ScanType.Pickup => ShipmentStatus.PickedUp,
        ScanType.InTransit => ShipmentStatus.InTransit,
        ScanType.Delivered => ShipmentStatus.Delivered,
        ScanType.Failed => ShipmentStatus.Failed,
        _ => throw new DomainException($"Scan type '{scanType}' is not valid."),
    };

    // Delivered and Failed are the two possible ends, so they share the last stage.
    private static int Stage(ShipmentStatus status) =>
        status == ShipmentStatus.Failed ? (int)ShipmentStatus.Delivered : (int)status;
}
