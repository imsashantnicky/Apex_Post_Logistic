namespace ApexPost.Application.Tracking;

/// <summary>Read-only views of tracked shipments and their scans, projected straight from storage.</summary>
public interface ITrackingQueries
{
    /// <summary>One page of shipments ordered by tracking number.</summary>
    Task<DTOs.PagedResult<ShipmentView>> ListAsync(ShipmentListFilter filter, CancellationToken cancellationToken);

    /// <summary>The shipment with this tracking number, or null.</summary>
    Task<ShipmentView?> FindAsync(string trackingNumber, CancellationToken cancellationToken);

    /// <summary>The scans of the shipment, oldest first, or null when the shipment does not exist.</summary>
    Task<IReadOnlyList<ScanView>?> FindScansAsync(string trackingNumber, CancellationToken cancellationToken);
}
