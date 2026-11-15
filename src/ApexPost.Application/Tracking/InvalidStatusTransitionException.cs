using ApexPost.Domain.Tracking;

namespace ApexPost.Application.Tracking;

/// <summary>Thrown when a status change would break the shipment lifecycle (HTTP 409).</summary>
public sealed class InvalidStatusTransitionException : Exception
{
    public InvalidStatusTransitionException()
    {
    }

    public InvalidStatusTransitionException(string message)
        : base(message)
    {
    }

    public InvalidStatusTransitionException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public InvalidStatusTransitionException(string trackingNumber, ShipmentStatus from, ShipmentStatus to)
        : base($"Shipment {trackingNumber} cannot move from {from} to {to}; a shipment only moves forward.")
    {
        From = from;
        To = to;
    }

    /// <summary>The status the shipment has.</summary>
    public ShipmentStatus? From { get; }

    /// <summary>The status that was asked for.</summary>
    public ShipmentStatus? To { get; }
}
