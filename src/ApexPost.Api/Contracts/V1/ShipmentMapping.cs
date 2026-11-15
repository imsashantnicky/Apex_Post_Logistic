using ApexPost.Application.DTOs;
using ApexPost.Application.Tracking;
using UploadResult = ApexPost.Application.Tracking.ScanUploadResult;

namespace ApexPost.Api.Contracts.V1;

/// <summary>
/// The only translation between the v1 contracts and the application. Requests become commands, never entities;
/// views become responses. It runs after [ApiController] validation, so required values are present.
/// </summary>
public static class ShipmentMapping
{
    /// <summary>A create request as the command of the tracking service.</summary>
    public static ShipmentCommand ToCommand(this CreateShipmentRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return new ShipmentCommand(
            request.RecipientName,
            request.RecipientPhone,
            request.RecipientAddress,
            request.RecipientCity,
            request.WeightKg);
    }

    /// <summary>A replace request as the command of the tracking service.</summary>
    public static ShipmentCommand ToCommand(this UpdateShipmentRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return new ShipmentCommand(
            request.RecipientName,
            request.RecipientPhone,
            request.RecipientAddress,
            request.RecipientCity,
            request.WeightKg);
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

    /// <summary>A shipment view as the v1 response.</summary>
    public static ShipmentResponse ToResponse(this ShipmentView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        return new ShipmentResponse(
            view.TrackingNumber,
            view.RecipientName,
            view.RecipientPhone,
            view.RecipientAddress,
            view.RecipientCity,
            view.WeightKg,
            view.Status,
            view.CreatedAtUtc,
            view.Version);
    }

    /// <summary>A stored scan as the v1 response.</summary>
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

    /// <summary>A page of shipment views as the v1 page.</summary>
    public static PagedResponse<ShipmentResponse> ToResponse(this PagedResult<ShipmentView> page)
    {
        ArgumentNullException.ThrowIfNull(page);
        return new PagedResponse<ShipmentResponse>(
            [.. page.Items.Select(ToResponse)],
            page.Page,
            page.PageSize,
            page.TotalCount,
            page.TotalPages,
            page.HasNext);
    }

    /// <summary>The outcome of a scan upload as the v1 response.</summary>
    public static ScanUploadResult ToResponse(this UploadResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return new ScanUploadResult(
            result.Accepted,
            result.DuplicatesSkipped,
            [.. result.Rejected.Select(rejected => new RejectedScanResponse(rejected.ScanId, rejected.Reason))]);
    }
}
