using ApexPost.Domain.Common;

namespace ApexPost.Domain.Tracking;

/// <summary>
/// A shipment a courier tracks with an <c>APX-########</c> number. It starts as Created and its status only moves
/// forward (<see cref="ShipmentStatusRules"/>). Separate from the counter's shipping lines of Week 3.
/// </summary>
public sealed class TrackedShipment : Entity<Guid>
{
    /// <summary>The heaviest shipment a courier takes.</summary>
    public const decimal MaximumWeightKg = 30m;

    /// <summary>Weights are kept to the gram.</summary>
    public const int WeightDecimals = 3;

    /// <summary>The version of a shipment that was never changed.</summary>
    public const int FirstVersion = 1;

    private TrackedShipment(Guid id, string trackingNumber, DateTime createdAtUtc)
        : base(id)
    {
        TrackingNumber = trackingNumber;
        CreatedAtUtc = createdAtUtc;
        Status = ShipmentStatus.Created;
    }

    // Only for persistence frameworks.
    private TrackedShipment()
    {
    }

    public string TrackingNumber { get; private set; } = string.Empty;

    public Recipient Recipient { get; private set; } = null!;

    public decimal WeightKg { get; private set; }

    public ShipmentStatus Status { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>True when Delivered or Failed.</summary>
    public bool IsFinal => ShipmentStatusRules.IsFinal(Status);

    /// <summary>Registers a new shipment in status Created.</summary>
    public static TrackedShipment Create(
        string trackingNumber,
        Recipient recipient,
        decimal weightKg,
        DateTime createdAtUtc)
    {
        if (!TrackingFormats.IsTrackingNumber(trackingNumber))
        {
            throw new DomainException("Tracking number must be APX- followed by 8 digits.");
        }

        var shipment = new TrackedShipment(Guid.CreateVersion7(), trackingNumber, Utc(createdAtUtc))
        {
            Version = FirstVersion,
        };
        shipment.Replace(recipient, weightKg);
        return shipment;
    }

    /// <summary>Replaces the recipient and the weight. A final shipment cannot change.</summary>
    public void Replace(Recipient recipient, decimal weightKg)
    {
        if (IsFinal)
        {
            throw new DomainException($"Shipment {TrackingNumber} is {Status} and can no longer change.");
        }

        Recipient = Guard.Required(recipient, "Recipient");
        WeightKg = ValidWeight(weightKg);
    }

    /// <summary>Moves the status forward; any other move breaks the lifecycle.</summary>
    public void MoveTo(ShipmentStatus status)
    {
        if (!ShipmentStatusRules.CanMove(Status, status))
        {
            throw new DomainException($"Shipment {TrackingNumber} cannot move from {Status} to {status}.");
        }

        Status = status;
    }

    internal static DateTime Utc(DateTime value) =>
        value.Kind == DateTimeKind.Utc ? value : throw new DomainException("Times must be given in UTC.");

    private static decimal ValidWeight(decimal weightKg) =>
        weightKg is > 0m and <= MaximumWeightKg && decimal.Round(weightKg, WeightDecimals) == weightKg
            ? weightKg
            : throw new DomainException("Weight must be above 0 and at most 30 kg, with at most 3 decimals.");
}
