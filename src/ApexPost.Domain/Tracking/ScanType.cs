namespace ApexPost.Domain.Tracking;

/// <summary>What a courier recorded when scanning a shipment. Same names as on the courier device.</summary>
public enum ScanType
{
    /// <summary>The courier collected the shipment.</summary>
    Pickup = 1,

    /// <summary>The shipment passed a hub or vehicle.</summary>
    InTransit = 2,

    /// <summary>The shipment was handed to the recipient.</summary>
    Delivered = 3,

    /// <summary>A delivery attempt did not succeed.</summary>
    Failed = 4,
}
