using ApexPost.Courier.Domain.Enums;

namespace ApexPost.Courier.ApiClient.Contracts;

/// <summary>One scan in an upload batch.</summary>
/// <param name="ScanId">Idempotency key, <c>scan::{Guid}</c>.</param>
/// <param name="TrackingNumber">Tracking number of the scanned parcel.</param>
/// <param name="ScanType">Kind of scan.</param>
/// <param name="CourierId">Courier id, must match the signed-in courier.</param>
/// <param name="Location">Where the scan happened.</param>
/// <param name="ScannedAtUtc">When the parcel was scanned (UTC, not in the future).</param>
/// <param name="Notes">Optional courier notes.</param>
/// <param name="RecipientSignatureName">Required by the API when <paramref name="ScanType"/> is Delivered.</param>
public sealed record ScanItem(
    string ScanId,
    string TrackingNumber,
    ScanType ScanType,
    string CourierId,
    string Location,
    DateTime ScannedAtUtc,
    string? Notes,
    string? RecipientSignatureName);
