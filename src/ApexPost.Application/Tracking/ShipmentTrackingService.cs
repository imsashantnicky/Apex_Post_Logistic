using ApexPost.Application.DTOs;
using ApexPost.Application.Exceptions;
using ApexPost.Domain.Common;
using ApexPost.Domain.Tracking;

namespace ApexPost.Application.Tracking;

/// <summary>
/// The tracking use cases. Reads go through <see cref="ITrackingQueries"/>; every write loads the shipment through
/// the repository, applies the domain rules and commits once through <see cref="ITrackingUnitOfWork"/>. A scan
/// upload is accepted only from the courier who recorded every scan in it (<see cref="ICurrentCourier"/>).
/// </summary>
public sealed class ShipmentTrackingService(
    ITrackedShipmentRepository shipments,
    ITrackingQueries queries,
    ITrackingUnitOfWork unitOfWork,
    ICurrentCourier courier,
    TimeProvider time) : IShipmentTrackingService
{
    private const string UnknownTrackingNumber = "Unknown tracking number.";
    private const string NotACourier = "Only a signed-in courier can upload scans.";

    public Task<PagedResult<ShipmentView>> ListAsync(
        int page,
        int pageSize,
        ShipmentStatus? status,
        string? city,
        CancellationToken cancellationToken)
    {
        TrackingLimits.CheckPage(page, pageSize);
        if (status is { } wanted && !Enum.IsDefined(wanted))
        {
            throw new ValidationFailedException("status", $"Status '{wanted}' is not valid.");
        }

        var cityFilter = string.IsNullOrWhiteSpace(city) ? null : city.Trim();
        return queries.ListAsync(new ShipmentListFilter(page, pageSize, status, cityFilter), cancellationToken);
    }

    public async Task<ShipmentView> GetAsync(string trackingNumber, CancellationToken cancellationToken) =>
        await queries.FindAsync(trackingNumber, cancellationToken).ConfigureAwait(false)
        ?? throw NotFound(trackingNumber);

    public async Task<ShipmentView> CreateAsync(
        string trackingNumber,
        ShipmentCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var shipment = TrackedShipment.Create(trackingNumber, RecipientOf(command), command.WeightKg, Now);
        if (await shipments.FindAsync(trackingNumber, cancellationToken).ConfigureAwait(false) is not null)
        {
            throw new DuplicateKeyException($"Shipment {trackingNumber} already exists.");
        }

        shipments.Add(shipment);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return await GetAsync(trackingNumber, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ShipmentView> ReplaceAsync(
        string trackingNumber,
        ShipmentCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var shipment = await LoadAsync(trackingNumber, cancellationToken).ConfigureAwait(false);
        if (shipment.IsFinal)
        {
            throw new ShipmentLockedException(
                $"Shipment {trackingNumber} is {shipment.Status} and can no longer change.");
        }

        shipment.Replace(RecipientOf(command), command.WeightKg);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return await GetAsync(trackingNumber, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ShipmentView> ChangeStatusAsync(
        string trackingNumber,
        ShipmentStatus status,
        CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(status))
        {
            throw new ValidationFailedException("status", $"Status '{status}' is not valid.");
        }

        var shipment = await LoadAsync(trackingNumber, cancellationToken).ConfigureAwait(false);
        if (!ShipmentStatusRules.CanMove(shipment.Status, status))
        {
            throw new InvalidStatusTransitionException(trackingNumber, shipment.Status, status);
        }

        shipment.MoveTo(status);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return await GetAsync(trackingNumber, cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteAsync(string trackingNumber, CancellationToken cancellationToken)
    {
        var shipment = await LoadAsync(trackingNumber, cancellationToken).ConfigureAwait(false);
        if (shipment.Status != ShipmentStatus.Created)
        {
            throw new ShipmentLockedException(
                $"Shipment {trackingNumber} is {shipment.Status}; only a Created shipment can be deleted.");
        }

        shipments.Remove(shipment);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task<ScanUploadResult> UploadScansAsync(
        IReadOnlyList<ScanUpload> scans,
        CancellationToken cancellationToken)
    {
        CheckBatch(scans);
        return UploadAsync(scans, onlyShipment: null, cancellationToken);
    }

    public async Task<ScanUploadResult> UploadShipmentScansAsync(
        string trackingNumber,
        IReadOnlyList<ScanUpload> scans,
        CancellationToken cancellationToken)
    {
        CheckBatch(scans);
        _ = await GetAsync(trackingNumber, cancellationToken).ConfigureAwait(false);
        return await UploadAsync(scans, trackingNumber, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ScanView>> GetScansAsync(
        string trackingNumber,
        CancellationToken cancellationToken) =>
        await queries.FindScansAsync(trackingNumber, cancellationToken).ConfigureAwait(false)
        ?? throw NotFound(trackingNumber);

    /// <summary>
    /// A parallel upload of the same scans, or of scans of the same shipment, can commit first. The database then
    /// refuses this commit (unique scan id or shipment version), and one fresh pass counts what the other one stored
    /// as duplicates.
    /// </summary>
    private async Task<ScanUploadResult> UploadAsync(
        IReadOnlyList<ScanUpload> scans,
        string? onlyShipment,
        CancellationToken cancellationToken)
    {
        try
        {
            return await UploadOnceAsync(scans, onlyShipment, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is DuplicateKeyException or ConcurrencyConflictException)
        {
            unitOfWork.DiscardChanges();
            return await UploadOnceAsync(scans, onlyShipment, cancellationToken).ConfigureAwait(false);
        }
    }

    // One query finds the scan ids already stored. A scan id repeated in the batch counts once: the first copy is
    // handled and every later copy is a duplicate. Oldest scan first, so a Pickup in the same batch is applied
    // before the Delivered that follows it.
    private async Task<ScanUploadResult> UploadOnceAsync(
        IReadOnlyList<ScanUpload> scans,
        string? onlyShipment,
        CancellationToken cancellationToken)
    {
        var unique = scans.DistinctBy(scan => scan.ScanId, StringComparer.Ordinal).ToList();
        var stored = await shipments
            .FindStoredScanIdsAsync(unique.ConvertAll(scan => scan.ScanId), cancellationToken)
            .ConfigureAwait(false);
        var loaded = new Dictionary<string, TrackedShipment?>(StringComparer.Ordinal);
        var rejected = new List<RejectedScan>();
        var accepted = 0;
        foreach (var scan in unique.Where(scan => !stored.Contains(scan.ScanId)).OrderBy(scan => scan.ScannedAtUtc))
        {
            var reason = await TryRecordAsync(scan, onlyShipment, loaded, cancellationToken).ConfigureAwait(false);
            if (reason is null)
            {
                accepted++;
            }
            else
            {
                rejected.Add(new RejectedScan(scan.ScanId, reason));
            }
        }

        if (accepted > 0)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return new ScanUploadResult(accepted, scans.Count - accepted - rejected.Count, rejected);
    }

    /// <summary>
    /// Stages the scan and moves its shipment forward, or returns why the scan is refused. A scan may repeat the
    /// current status (one InTransit scan per hub) but never ask for an earlier status or follow Delivered or Failed.
    /// </summary>
    private async Task<string?> TryRecordAsync(
        ScanUpload scan,
        string? onlyShipment,
        Dictionary<string, TrackedShipment?> loaded,
        CancellationToken cancellationToken)
    {
        if (!TrackingFormats.IsTrackingNumber(scan.TrackingNumber))
        {
            return "Tracking number must be APX- followed by 8 digits.";
        }

        if (onlyShipment is not null && !string.Equals(scan.TrackingNumber, onlyShipment, StringComparison.Ordinal))
        {
            return $"The scan is for {scan.TrackingNumber}, not for shipment {onlyShipment}.";
        }

        if (!loaded.TryGetValue(scan.TrackingNumber, out var shipment))
        {
            shipment = await shipments.FindAsync(scan.TrackingNumber, cancellationToken).ConfigureAwait(false);
            loaded[scan.TrackingNumber] = shipment;
        }

        if (shipment is null)
        {
            return UnknownTrackingNumber;
        }

        var target = ShipmentStatusRules.StatusFor(scan.ScanType);
        var moves = ShipmentStatusRules.CanMove(shipment.Status, target);
        if (!moves && (shipment.IsFinal || target != shipment.Status))
        {
            return $"A {scan.ScanType} scan cannot move shipment {shipment.TrackingNumber} "
                + $"from {shipment.Status} to {target}.";
        }

        try
        {
            shipments.AddScan(TrackingScan.Record(shipment, scan.Details, Now));
        }
        catch (DomainException exception)
        {
            return exception.Message;
        }

        if (moves)
        {
            shipment.MoveTo(target);
        }

        return null;
    }

    /// <summary>
    /// The size limits, then ownership: the caller must be a courier, and every scan with a well-formed courier id
    /// must carry the caller's id, or the whole batch is refused with <see cref="ForbiddenException"/> (403) before
    /// anything is read or stored. A malformed courier id claims no owner; that scan is rejected later with a reason,
    /// like any other invalid scan.
    /// </summary>
    private void CheckBatch(IReadOnlyList<ScanUpload> scans)
    {
        ArgumentNullException.ThrowIfNull(scans);
        TrackingLimits.CheckUpload(scans.Count);
        var caller = courier.CourierId;
        if (!TrackingFormats.IsCourierId(caller))
        {
            throw new ForbiddenException(NotACourier);
        }

        var foreign = scans.FirstOrDefault(scan => TrackingFormats.IsCourierId(scan.CourierId)
            && !string.Equals(scan.CourierId, caller, StringComparison.Ordinal));
        if (foreign is not null)
        {
            throw new ForbiddenException(
                $"Courier {caller} cannot upload scan {foreign.ScanId}, which was recorded by {foreign.CourierId}.");
        }
    }

    private async Task<TrackedShipment> LoadAsync(string trackingNumber, CancellationToken cancellationToken) =>
        await shipments.FindAsync(trackingNumber, cancellationToken).ConfigureAwait(false)
        ?? throw NotFound(trackingNumber);

    private DateTime Now => time.GetUtcNow().UtcDateTime;

    private static Recipient RecipientOf(ShipmentCommand command) =>
        new(command.RecipientName, command.RecipientPhone, command.RecipientAddress, command.RecipientCity);

    private static NotFoundException NotFound(string trackingNumber) =>
        new($"Shipment {trackingNumber} does not exist.");
}
