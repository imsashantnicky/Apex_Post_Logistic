using ApexPost.Api.Contracts.V2;
using ApexPost.Api.Security;
using ApexPost.Application.Tracking;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScanUploadResult = ApexPost.Api.Contracts.V1.ScanUploadResult;

namespace ApexPost.Api.Controllers.V2;

/// <summary>
/// The scans of one shipment in the v2 contract, with scan types by name as the courier device stores them. Same
/// service and idempotency rules as v1.
/// </summary>
[Authorize]
[ApiController]
[ApiVersion(ApiRoutes.Version2)]
[Route(ApiRoutes.VersionedShipmentScans)]
[Tags(ApiRoutes.ScansTag)]
[ProducesJson]
public sealed class ShipmentScansController(IShipmentTrackingService tracking) : ControllerBase
{
    /// <summary>Upload scans of a shipment</summary>
    /// <param name="trackingNumber" example="APX-20000001">APX- followed by 8 digits.</param>
    /// <param name="cancellationToken">Cancelled when the client goes away.</param>
    /// <param name="request">1 to 100 scans.</param>
    /// <response code="200">The counts of accepted, duplicate and rejected scans.</response>
    /// <response code="400">The batch or a scan field is missing or invalid.</response>
    /// <response code="404">No shipment has this tracking number.</response>
    /// <response code="415">The body is not application/json.</response>
    /// <response code="401">No valid bearer token: missing, expired or not signed with the API's key.</response>
    /// <response code="403">The token is not a Courier token, or a scan was recorded by another courier.</response>
    /// <response code="500">An unexpected server error.</response>
    [HttpPost(Name = ApiRoutes.UploadScans)]
    [Authorize(Roles = ApiRoles.Courier)]
    [EndpointSummary("Upload scans of a shipment")]
    [EndpointDescription(
        "Stores a batch of 1 to 100 courier scans. The scanId (scan::{GUID}) is the idempotency key: a scan that " +
        "is already stored, or repeated in the batch, is counted once under duplicatesSkipped, so the same batch " +
        "can be sent again safely. A scan of another shipment or one that would move the status backwards is " +
        "listed under rejected with a reason; the valid scans are still stored.")]
    [Consumes(ApiRoutes.Json)]
    [ProducesResponseType<ScanUploadResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, ApiRoutes.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ApiRoutes.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status415UnsupportedMediaType, ApiRoutes.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, ApiRoutes.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, ApiRoutes.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, ApiRoutes.ProblemJson)]
    public async Task<ActionResult<ScanUploadResult>> Upload(
        string trackingNumber,
        ScanUploadRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var result = await tracking.UploadShipmentScansAsync(trackingNumber, request.ToUploads(), cancellationToken);
        return Ok(result.ToResponse());
    }

    /// <summary>Get the scans of a shipment</summary>
    /// <param name="trackingNumber" example="APX-20000001">APX- followed by 8 digits.</param>
    /// <param name="cancellationToken">Cancelled when the client goes away.</param>
    /// <response code="200">The scans, oldest first; empty when none were uploaded yet.</response>
    /// <response code="404">No shipment has this tracking number.</response>
    /// <response code="401">No valid bearer token: missing, expired or not signed with the API's key.</response>
    /// <response code="500">An unexpected server error.</response>
    [HttpGet(Name = ApiRoutes.GetScans)]
    [EndpointSummary("Get the scans of a shipment")]
    [EndpointDescription("Returns the scan history of one shipment, oldest first by scannedAtUtc.")]
    [ProducesResponseType<IReadOnlyList<ScanResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ApiRoutes.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, ApiRoutes.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, ApiRoutes.ProblemJson)]
    public async Task<ActionResult<IReadOnlyList<ScanResponse>>> List(
        string trackingNumber,
        CancellationToken cancellationToken)
    {
        var scans = await tracking.GetScansAsync(trackingNumber, cancellationToken);
        return Ok(scans.Select(scan => scan.ToResponse()).ToList());
    }
}
