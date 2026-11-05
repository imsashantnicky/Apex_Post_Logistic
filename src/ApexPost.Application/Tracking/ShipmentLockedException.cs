namespace ApexPost.Application.Tracking;

/// <summary>
/// Thrown when the status of a shipment no longer allows a change: a delete after Created, or an edit after
/// Delivered or Failed (HTTP 409).
/// </summary>
public sealed class ShipmentLockedException : Exception
{
    public ShipmentLockedException()
    {
    }

    public ShipmentLockedException(string message)
        : base(message)
    {
    }

    public ShipmentLockedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
