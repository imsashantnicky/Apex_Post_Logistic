namespace ApexPost.Courier.Domain.Enums;

/// <summary>What happened to a shipment at the moment the courier scanned it.</summary>
public enum ScanType
{
    /// <summary>The courier collected the shipment from the sender or a counter.</summary>
    Pickup = 1,

    /// <summary>The shipment passed a hub or vehicle on its way to the recipient.</summary>
    InTransit = 2,

    /// <summary>The shipment was handed to the recipient.</summary>
    Delivered = 3,

    /// <summary>A delivery attempt did not succeed.</summary>
    Failed = 4,
}
