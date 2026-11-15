using ApexPost.Courier.Domain.Scans;

namespace ApexPost.Courier.Domain.Ports;

/// <summary>Stores scans on the device. Implementations live outside the domain (Couchbase Lite in Part A).</summary>
public interface IScanRepository
{
    /// <summary>Saves a new scan. Fails if a scan with the same id already exists.</summary>
    Task CreateAsync(ShipmentScan scan, CancellationToken ct);

    /// <summary>Returns the scan with this id, or <see langword="null"/> when there is none.</summary>
    Task<ShipmentScan?> GetByIdAsync(string id, CancellationToken ct);

    /// <summary>Saves changes to an existing scan. Fails if the scan does not exist.</summary>
    Task UpdateAsync(ShipmentScan scan, CancellationToken ct);

    /// <summary>Deletes the scan with this id.</summary>
    /// <returns><see langword="false"/> when there was nothing to delete.</returns>
    Task<bool> DeleteAsync(string id, CancellationToken ct);

    /// <summary>Returns every stored scan.</summary>
    Task<IReadOnlyList<ShipmentScan>> ListAllAsync(CancellationToken ct);
}
