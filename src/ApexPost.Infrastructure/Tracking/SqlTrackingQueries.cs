using ApexPost.Application.DTOs;
using ApexPost.Application.Tracking;
using ApexPost.Domain.Tracking;
using Microsoft.EntityFrameworkCore;

namespace ApexPost.Infrastructure.Tracking;

/// <summary>
/// Read-only projections of the tracking tables into views. Nothing is tracked, and the latest scan of a shipment is
/// a sub-query served by the (ShipmentId, ScannedAtUtc) index.
/// </summary>
public sealed class SqlTrackingQueries(TrackingDbContext database) : ITrackingQueries
{
    public async Task<PagedResult<ShipmentView>> ListAsync(
        ShipmentListFilter filter,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filter);
        var shipments = database.Shipments.AsNoTracking();
        if (filter.Status is { } status)
        {
            shipments = shipments.Where(shipment => shipment.Status == status);
        }

        if (filter.City is { } city)
        {
            shipments = shipments.Where(shipment => shipment.Recipient.City == city);
        }

        var total = await shipments.CountAsync(cancellationToken).ConfigureAwait(false);
        var items = await Project(shipments.OrderBy(shipment => shipment.TrackingNumber)
                .Skip(filter.Skip)
                .Take(filter.PageSize))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return new PagedResult<ShipmentView>(items, filter.Page, filter.PageSize, total);
    }

    public Task<ShipmentView?> FindAsync(string trackingNumber, CancellationToken cancellationToken) =>
        Project(database.Shipments.AsNoTracking().Where(shipment => shipment.TrackingNumber == trackingNumber))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<ScanView>?> FindScansAsync(
        string trackingNumber,
        CancellationToken cancellationToken)
    {
        var exists = await database.Shipments.AsNoTracking()
            .AnyAsync(shipment => shipment.TrackingNumber == trackingNumber, cancellationToken)
            .ConfigureAwait(false);
        if (!exists)
        {
            return null;
        }

        return await (
                from scan in database.Scans.AsNoTracking()
                join shipment in database.Shipments on scan.ShipmentId equals shipment.Id
                where shipment.TrackingNumber == trackingNumber
                orderby scan.ScannedAtUtc, scan.ScanId
                select new ScanView(
                    scan.ScanId,
                    shipment.TrackingNumber,
                    scan.ScanType,
                    scan.CourierId,
                    scan.Location,
                    scan.ScannedAtUtc,
                    scan.Notes,
                    scan.ReceivedAtUtc))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private IQueryable<ShipmentView> Project(IQueryable<TrackedShipment> shipments) =>
        shipments.Select(shipment => new ShipmentView(
            shipment.TrackingNumber,
            shipment.Recipient.Name,
            shipment.Recipient.Phone,
            shipment.Recipient.Address,
            shipment.Recipient.City,
            shipment.WeightKg,
            shipment.Status,
            shipment.CreatedAtUtc,
            shipment.Version,
            database.Scans
                .Where(scan => scan.ShipmentId == shipment.Id)
                .OrderByDescending(scan => scan.ScannedAtUtc)
                .ThenByDescending(scan => scan.ScanId)
                .Select(scan => new ScanView(
                    scan.ScanId,
                    shipment.TrackingNumber,
                    scan.ScanType,
                    scan.CourierId,
                    scan.Location,
                    scan.ScannedAtUtc,
                    scan.Notes,
                    scan.ReceivedAtUtc))
                .FirstOrDefault()));
}
