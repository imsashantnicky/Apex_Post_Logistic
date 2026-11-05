using ApexPost.Api.Contracts.V1;
using ApexPost.Application.Tracking;
using Microsoft.AspNetCore.Mvc;
using ScanUploadResult = ApexPost.Api.Contracts.V1.ScanUploadResult;

namespace ApexPost.Api.Controllers;

/// <summary>
/// The scans of one shipment: an idempotent upload from the courier device and the scan history. Each action binds
/// the input, calls <see cref="IShipmentTrackingService"/> and returns the result.
/// </summary>
[ApiController]
[Route(ApiRoutes.ShipmentScans)]
[ProducesJson]
public sealed class ShipmentScansController(IShipmentTrackingService tracking) : ControllerBase
{
    /// <summary>
    /// Stores a batch of scans of this shipment. A scan id that is already stored, or repeated in the batch, is
    /// counted once as a duplicate, so the same batch can be sent again safely. A scan of another shipment or one
    /// that would move the status backwards is listed under rejected with a reason; the valid scans are still stored.
    /// </summary>
    /// <param name="trackingNumber">APX- followed by 8 digits.</param>
    /// <param name="request">1 to 100 scans.</param>
    /// <param name="cancellationToken">Cancelled when the client goes away.</param>
    [HttpPost(Name = ApiRoutes.UploadScans)]
    [Consumes(ApiRoutes.Json)]
    [ProducesResponseType<ScanUploadResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status415UnsupportedMediaType)]
    public async Task<ActionResult<ScanUploadResult>> Upload(
        string trackingNumber,
        ScanUploadRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var result = await tracking.UploadShipmentScansAsync(trackingNumber, request.ToUploads(), cancellationToken);
        return Ok(result.ToResponse());
    }

    /// <summary>The scans of this shipment, oldest first by scannedAtUtc.</summary>
    /// <param name="trackingNumber">APX- followed by 8 digits.</param>
    /// <param name="cancellationToken">Cancelled when the client goes away.</param>
    [HttpGet(Name = ApiRoutes.ListScans)]
    [ProducesResponseType<IReadOnlyList<ScanResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<ScanResponse>>> List(
        string trackingNumber,
        CancellationToken cancellationToken)
    {
        var scans = await tracking.GetScansAsync(trackingNumber, cancellationToken);
        return Ok(scans.Select(scan => scan.ToResponse()).ToList());
    }
}
