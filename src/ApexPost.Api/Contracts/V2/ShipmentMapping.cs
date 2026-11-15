using ApexPost.Application.DTOs;
using ApexPost.Application.Tracking;
using UploadResult = ApexPost.Application.Tracking.ScanUploadResult;
using V1Mapping = ApexPost.Api.Contracts.V1.ShipmentMapping;

namespace ApexPost.Api.Contracts.V2;

/// <summary>
/// The only translation between the v2 contracts and the application. Both versions call the same service with the
/// same commands; only the shape of the JSON differs. The paged list and the upload counts keep the v1 shape.
/// </summary>
public static class ShipmentMapping
{
    /// <summary>A create request as the command of the tracking service.</summary>
    public static ShipmentCommand ToCommand(this CreateShipmentRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ToCommand(request.Recipient, request.WeightKg);
    }

    /// <summary>A replace request as the command of the tracking service.</summary>
    public static ShipmentCommand ToCommand(this UpdateShipmentRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ToCommand(request.Recipient, request.WeightKg);
    }

    /// <summary>The scans of an upload request as service uploads, in the order sent.</summary>
    public static IReadOnlyList<ScanUpload> ToUploads(this ScanUploadRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return [.. request.Scans.Select(ToUpload)];
    }

    /// <summary>One scan of an upload request as a service upload.</summary>
    public static ScanUpload ToUpload(this ScanItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return new ScanUpload(
            item.ScanId,
            item.TrackingNumber,
            item.ScanType.GetValueOrDefault(),
            item.CourierId,
            item.Location,
            item.ScannedAtUtc.GetValueOrDefault(),
            item.Notes);
    }

    /// <summary>A shipment view as the v2 response: nested recipient and the latest scan (null without scans).</summary>
    public static ShipmentResponse ToResponse(this ShipmentView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        return new ShipmentResponse(
            view.TrackingNumber,
            new RecipientResponse(view.RecipientName, view.RecipientPhone, view.RecipientAddress, view.RecipientCity),
            view.WeightKg,
            view.Status,
            ToLatestScan(view.LatestScan),
            view.CreatedAtUtc,
            view.Version);
    }

    /// <summary>A stored scan as the v2 response.</summary>
    public static ScanResponse ToResponse(this ScanView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        return new ScanResponse(
            view.ScanId,
            view.TrackingNumber,
            view.ScanType,
            view.CourierId,
            view.Location,
            view.ScannedAtUtc,
            view.Notes,
            view.ReceivedAtUtc);
    }

    /// <summary>A page of shipment views as a page of v2 shipments.</summary>
    public static V1.PagedResponse<ShipmentResponse> ToResponse(this PagedResult<ShipmentView> page)
    {
        ArgumentNullException.ThrowIfNull(page);
        return new V1.PagedResponse<ShipmentResponse>(
            [.. page.Items.Select(ToResponse)],
            page.Page,
            page.PageSize,
            page.TotalCount,
            page.TotalPages,
            page.HasNext);
    }

    /// <summary>The outcome of a scan upload; the same <c>{ accepted, duplicatesSkipped, rejected }</c> as v1.</summary>
    public static V1.ScanUploadResult ToResponse(this UploadResult result) => V1Mapping.ToResponse(result);

    private static LatestScanResponse? ToLatestScan(ScanView? scan) =>
        scan is null ? null : new LatestScanResponse(scan.ScanType, scan.Location, scan.ScannedAtUtc);

    // [ApiController] validation runs first, so the recipient is present here.
    private static ShipmentCommand ToCommand(RecipientRequest? recipient, decimal weightKg)
    {
        ArgumentNullException.ThrowIfNull(recipient);
        return new ShipmentCommand(recipient.Name, recipient.Phone, recipient.Address, recipient.City, weightKg);
    }
}
