using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using ApexPost.Domain.Tracking;

namespace ApexPost.Api.Contracts.V2;

/// <summary>The body of PATCH /api/v2/shipments/{trackingNumber}/status: the status to move to, by name.</summary>
public sealed class UpdateStatusRequest
{
    /// <summary>
    /// The new status: Created, PickedUp, InTransit, OutForDelivery, Delivered or Failed. Only forward moves pass.
    /// </summary>
    /// <example>PickedUp</example>
    [Required]
    [EnumDataType(typeof(ShipmentStatus))]
    [JsonConverter(typeof(JsonStringEnumConverter<ShipmentStatus>))]
    public ShipmentStatus? Status { get; init; }
}
