namespace ApexPost.Application.DTOs;

/// <summary>The courier's answer for one tracking number.</summary>
public sealed record ParcelStatus(string TrackingNumber, string Status, string Location, DateTimeOffset UpdatedAt);
