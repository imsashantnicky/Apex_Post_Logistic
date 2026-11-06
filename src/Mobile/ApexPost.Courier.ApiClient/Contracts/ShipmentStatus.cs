namespace ApexPost.Courier.ApiClient.Contracts;

/// <summary>Shipment status in the v2 API. It travels as a string, for example <c>"InTransit"</c>.</summary>
public enum ShipmentStatus
{
    /// <summary>The shipment exists but has not been collected.</summary>
    Created = 1,

    /// <summary>A courier collected the shipment.</summary>
    PickedUp = 2,

    /// <summary>The shipment is moving between hubs.</summary>
    InTransit = 3,

    /// <summary>The shipment is on the last delivery route.</summary>
    OutForDelivery = 4,

    /// <summary>The shipment was delivered (final).</summary>
    Delivered = 5,

    /// <summary>The delivery failed (final).</summary>
    Failed = 6,
}
