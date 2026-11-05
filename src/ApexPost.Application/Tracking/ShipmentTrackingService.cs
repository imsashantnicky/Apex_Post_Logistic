using ApexPost.Application.DTOs;
using ApexPost.Application.Exceptions;
using ApexPost.Domain.Common;
using ApexPost.Domain.Tracking;

namespace ApexPost.Application.Tracking;

/// <summary>
/// The tracking use cases. Reads go through <see cref="ITrackingQueries"/>; every write loads the shipment through
/// the repository, applies the domain rules and commits once through <see cref="ITrackingUnitOfWork"/>.
/// </summary>
public sealed class ShipmentTrackingService(
    ITrackedShipmentRepository shipments,
    ITrackingQueries queries,
    ITrackingUnitOfWork unitOfWork,
    TimeProvider time) : IShipmentTrackingService
{
    private const string UnknownTrackingNumber = "Unknown tracking number.";

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

    /// <summary>
    /// A parallel upload of the same scans, or of scans of the same shipment, can commit first. The database then
    /// refuses this commit (unique scan id or shipment version), and one fresh pass sees what the other one stored.
    /// </summary>
    public async Task<ScanUploadResult> UploadScansAsync(
        IReadOnlyList<ScanUpload> scans,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scans);
        TrackingLimits.CheckUpload(scans.Count);
        try
        {
            return await UploadOnceAsync(scans, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is DuplicateKeyException or ConcurrencyConflictException)
        {
            unitOfWork.DiscardChanges();
            return await UploadOnceAsync(scans, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<ScanUploadResult> UploadShipmentScansAsync(
        string trackingNumber,
        IReadOnlyList<ScanUpload> scans,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scans);
        TrackingLimits.CheckUpload(scans.Count);
        _ = await GetAsync(trackingNumber, cancellationToken).ConfigureAwait(false);
        var foreign = scans
            .Select((scan, index) => (scan.TrackingNumber, Index: index))
            .Where(scan => !string.Equals(scan.TrackingNumber, trackingNumber, StringComparison.Ordinal))
            .ToDictionary(
                scan => $"scans[{scan.Index}].trackingNumber",
                _ => new[] { $"Every scan must belong to shipment {trackingNumber}." });
        if (foreign.Count > 0)
        {
            throw new ValidationFailedException(foreign);
        }

        return await UploadScansAsync(scans, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ScanView>> GetScansAsync(
        string trackingNumber,
        CancellationToken cancellationToken) =>
        await queries.FindScansAsync(trackingNumber, cancellationToken).ConfigureAwait(false)
        ?? throw NotFound(trackingNumber);

    // Oldest scan first, so a Pickup in the same batch is applied before the Delivered that follows it.
    private async Task<ScanUploadResult> UploadOnceAsync(
        IReadOnlyList<ScanUpload> scans,
        CancellationToken cancellationToken)
    {
        var scanIds = scans.Select(scan => scan.ScanId).Distinct(StringComparer.Ordinal).ToList();
        var stored = await shipments.FindStoredScanIdsAsync(scanIds, cancellationToken).ConfigureAwait(false);
        var accepted = new HashSet<string>(StringComparer.Ordinal);
        var loaded = new Dictionary<string, TrackedShipment?>(StringComparer.Ordinal);
        var rejected = new List<RejectedScan>();
        var duplicates = 0;
        foreach (var scan in scans.OrderBy(scan => scan.ScannedAtUtc))
        {
            if (stored.Contains(scan.ScanId) || accepted.Contains(scan.ScanId))
            {
                duplicates++;
                continue;
            }

            var reason = await TryRecordAsync(scan, loaded, cancellationToken).ConfigureAwait(false);
            if (reason is null)
            {
                accepted.Add(scan.ScanId);
            }
            else
            {
                rejected.Add(new RejectedScan(scan.ScanId, reason));
            }
        }

        if (accepted.Count > 0)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return new ScanUploadResult(accepted.Count, duplicates, rejected);
    }

    /// <summary>
    /// Stages the scan, moves its shipment forward if it can, and returns the refusal reason or null.
    /// </summary>
    private async Task<string?> TryRecordAsync(
        ScanUpload scan,
        Dictionary<string, TrackedShipment?> loaded,
        CancellationToken cancellationToken)
    {
        if (!TrackingFormats.IsTrackingNumber(scan.TrackingNumber))
        {
            return "Tracking number must be APX- followed by 8 digits.";
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

        try
        {
            shipments.AddScan(TrackingScan.Record(shipment, scan.Details, Now));
        }
        catch (DomainException exception)
        {
            return exception.Message;
        }

        // A scan that would move the shipment back, or keep it where it is, stays in the history only.
        var target = ShipmentStatusRules.StatusFor(scan.ScanType);
        if (ShipmentStatusRules.CanMove(shipment.Status, target))
        {
            shipment.MoveTo(target);
        }

        return null;
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
