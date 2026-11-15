using System.ComponentModel.DataAnnotations;
using ApexPost.Domain.Tracking;

namespace ApexPost.Api.Contracts.V1;

/// <summary>The body of PATCH /api/shipments/{trackingNumber}/status: the status to move to.</summary>
public sealed class UpdateStatusRequest
{
    /// <summary>
    /// The new status (v1 sends the number: 1 Created, 2 PickedUp, 3 InTransit, 4 OutForDelivery, 5 Delivered,
    /// 6 Failed). Only forward moves pass.
    /// </summary>
    /// <example>2</example>
    [Required]
    [EnumDataType(typeof(ShipmentStatus))]
    public ShipmentStatus? Status { get; init; }
}
