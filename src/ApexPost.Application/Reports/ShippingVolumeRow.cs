using ApexPost.Domain.Common;
using ApexPost.Domain.Shipping;

namespace ApexPost.Application.Reports;

/// <summary>Paid shipments per destination zone and service level.</summary>
public sealed record ShippingVolumeRow(
    string ZoneCode,
    ServiceLevel Level,
    int Shipments,
    decimal ChargeableKg,
    Money Revenue);
