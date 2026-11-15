using ApexPost.Application.DTOs;
using ApexPost.Domain.Tracking;

namespace ApexPost.Application.Tracking;

/// <summary>
/// Every use case of shipment tracking. All business rules live behind this interface; the API only maps its
/// results and exceptions: <see cref="Exceptions.NotFoundException"/> 404,
/// <see cref="Exceptions.DuplicateKeyException"/>, <see cref="InvalidStatusTransitionException"/>,
/// <see cref="ShipmentLockedException"/> and
/// <see cref="Exceptions.ConcurrencyConflictException"/> 409, <see cref="Exceptions.ValidationFailedException"/> and
/// <see cref="Domain.Common.DomainException"/> 400.
/// </summary>
public interface IShipmentTrackingService
{
    /// <summary>One page of shipments, optionally of one status and one city (case-insensitive).</summary>
    Task<PagedResult<ShipmentView>> ListAsync(
        int page,
        int pageSize,
        ShipmentStatus? status,
        string? city,
        CancellationToken cancellationToken);

    /// <summary>One shipment; unknown tracking numbers throw NotFoundException.</summary>
    Task<ShipmentView> GetAsync(string trackingNumber, CancellationToken cancellationToken);

    /// <summary>Registers a Created shipment; a tracking number in use throws DuplicateKeyException.</summary>
    Task<ShipmentView> CreateAsync(
        string trackingNumber,
        ShipmentCommand command,
        CancellationToken cancellationToken);

    /// <summary>Replaces recipient and weight until the shipment is Delivered or Failed.</summary>
    Task<ShipmentView> ReplaceAsync(
        string trackingNumber,
        ShipmentCommand command,
        CancellationToken cancellationToken);

    /// <summary>Moves the status forward; any other move throws InvalidStatusTransitionException.</summary>
    Task<ShipmentView> ChangeStatusAsync(
        string trackingNumber,
        ShipmentStatus status,
        CancellationToken cancellationToken);

    /// <summary>Deletes a shipment that is still Created.</summary>
    Task DeleteAsync(string trackingNumber, CancellationToken cancellationToken);

    /// <summary>
    /// Stores a batch of device scans. A scan id already stored, or repeated in the batch, is skipped as a duplicate,
    /// so the same upload can be sent again safely. Each stored scan moves its shipment forward; a scan that would
    /// move it back or past Delivered/Failed is rejected with a reason, and the valid scans are still stored.
    /// Accepted + duplicates + rejected always equals the number of scans sent. Only the courier who recorded the
    /// scans may upload them: any other caller gets <see cref="Exceptions.ForbiddenException"/> (403).
    /// </summary>
    Task<ScanUploadResult> UploadScansAsync(IReadOnlyList<ScanUpload> scans, CancellationToken cancellationToken);

    /// <summary>
    /// Stores a batch of scans of one shipment, with the same rules as <see cref="UploadScansAsync"/>. An unknown
    /// tracking number throws NotFoundException; a scan of another shipment is rejected with a reason.
    /// </summary>
    Task<ScanUploadResult> UploadShipmentScansAsync(
        string trackingNumber,
        IReadOnlyList<ScanUpload> scans,
        CancellationToken cancellationToken);

    /// <summary>The scans of one shipment, oldest first.</summary>
    Task<IReadOnlyList<ScanView>> GetScansAsync(string trackingNumber, CancellationToken cancellationToken);
}
