namespace ApexPost.Domain.Tracking;

/// <summary>Where a tracked shipment is in its life. The order of the values is the order of the lifecycle.</summary>
public enum ShipmentStatus
{
    /// <summary>The shipment is registered but no courier has collected it yet.</summary>
    Created = 1,

    /// <summary>A courier collected the shipment.</summary>
    PickedUp = 2,

    /// <summary>The shipment is moving between hubs.</summary>
    InTransit = 3,

    /// <summary>The shipment is on the vehicle that delivers it.</summary>
    OutForDelivery = 4,

    /// <summary>The recipient received the shipment. Final.</summary>
    Delivered = 5,

    /// <summary>The delivery did not succeed. Final.</summary>
    Failed = 6,
}
