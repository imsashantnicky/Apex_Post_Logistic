using ApexPost.Domain.Tracking;

namespace ApexPost.Application.Tracking;

/// <summary>One checked page of the shipment list, optionally limited to one status and one city.</summary>
public sealed record ShipmentListFilter(int Page, int PageSize, ShipmentStatus? Status, string? City)
{
    public int Skip => (Page - 1) * PageSize;
}
