using System.ComponentModel.DataAnnotations;
using ApexPost.Domain.Tracking;

namespace ApexPost.Api.Contracts.V1;

/// <summary>The body of PATCH /api/v1/shipments/{trackingNumber}/status: the status to move to.</summary>
public sealed class UpdateStatusRequest
{
    /// <summary>The new status (v1 sends the number, 1 Created to 6 Failed). Only forward moves pass.</summary>
    [Required]
    [EnumDataType(typeof(ShipmentStatus))]
    public ShipmentStatus? Status { get; init; }
}
