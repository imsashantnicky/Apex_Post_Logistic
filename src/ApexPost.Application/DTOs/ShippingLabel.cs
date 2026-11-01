using ApexPost.Domain.Branches;
using ApexPost.Domain.Shipping;

namespace ApexPost.Application.DTOs;

/// <summary>What the printed shipping label shows, for example reference APX-0F3A9C12-1.</summary>
public sealed record ShippingLabel(
    string Reference,
    string TrackingNumber,
    BranchCode FromBranch,
    string FromBranchName,
    Address Recipient,
    Weight Weight,
    ShipmentType Type,
    ServiceLevel Level,
    string ZoneCode);
