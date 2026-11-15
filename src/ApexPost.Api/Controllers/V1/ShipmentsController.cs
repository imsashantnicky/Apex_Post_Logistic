using System.ComponentModel.DataAnnotations;
using ApexPost.Api.Contracts.V1;
using ApexPost.Api.Security;
using ApexPost.Application.Tracking;
using ApexPost.Domain.Tracking;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApexPost.Api.Controllers.V1;

/// <summary>
/// Shipments in the deprecated v1 contract (Part B, unchanged): list, read, create, replace, change status and delete. Each action binds the input, calls
/// <see cref="IShipmentTrackingService"/> and returns the result; errors are thrown by the service and written as
/// ProblemDetails by the global exception handler.
/// </summary>
/// <remarks>
/// The OpenAPI metadata of every action: the route name is the operationId, <c>[EndpointSummary]</c> and
/// <c>[EndpointDescription]</c> fill summary and description, and one <c>[ProducesResponseType]</c> per status code
/// the action can answer, errors as application/problem+json. Every action answers on /api/v1/shipments and on the
/// unversioned /api/shipments alias; only the versioned route is named and documented.
/// </remarks>
[Authorize]
[ApiController]
[ApiVersion(ApiRoutes.Version1, Deprecated = true)]
[Tags(ApiRoutes.ShipmentsTag)]
[ProducesJson]
public sealed class ShipmentsController(IShipmentTrackingService tracking) : ControllerBase
{
    /// <summary>List shipments</summary>
    /// <param name="page" example="1">The page number, 1 or more.</param>
    /// <param name="pageSize" example="20">Shipments per page, 1 to 50 (default 20).</param>
    /// <param name="status" example="3">Only shipments with this status (name or number).</param>
    /// <param name="city" example="Harbour Point">Only shipments to this city, ignoring case.</param>
    /// <param name="cancellationToken">Cancelled when the client goes away.</param>
    /// <response code="200">One page of shipments, ordered by tracking number.</response>
    /// <response code="400">page or pageSize out of range, or an unknown status.</response>
    /// <response code="401">No valid bearer token: missing, expired or not signed with the API's key.</response>
    /// <response code="500">An unexpected server error.</response>
    [HttpGet(ApiRoutes.VersionedShipments, Name = ApiRoutes.ListShipmentsV1)]
    [HttpGet(ApiRoutes.Shipments)]
    [EndpointSummary("List shipments")]
    [EndpointDescription(
        "Returns one page of shipments, optionally only one status and one city. pageSize is 1 to 50 (default " +
        "20); a larger value is answered with 400, never silently cut.")]
    [ProducesResponseType<PagedResponse<ShipmentResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, ApiRoutes.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, ApiRoutes.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, ApiRoutes.ProblemJson)]
    public async Task<ActionResult<PagedResponse<ShipmentResponse>>> List(
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery, Range(1, TrackingLimits.MaximumPageSize)] int pageSize = TrackingLimits.DefaultPageSize,
        [FromQuery] ShipmentStatus? status = null,
        [FromQuery] string? city = null,
        CancellationToken cancellationToken = default)
    {
        var result = await tracking.ListAsync(page, pageSize, status, city, cancellationToken);
        return Ok(result.ToResponse());
    }

    /// <summary>Get a shipment</summary>
    /// <param name="trackingNumber" example="APX-20000001">APX- followed by 8 digits.</param>
    /// <param name="cancellationToken">Cancelled when the client goes away.</param>
    /// <response code="200">The shipment.</response>
    /// <response code="404">No shipment has this tracking number, or it is not APX- followed by 8 digits.</response>
    /// <response code="401">No valid bearer token: missing, expired or not signed with the API's key.</response>
    /// <response code="500">An unexpected server error.</response>
    [HttpGet(ApiRoutes.VersionedShipment, Name = ApiRoutes.GetShipmentV1)]
    [HttpGet(ApiRoutes.Shipment)]
    [EndpointSummary("Get a shipment")]
    [EndpointDescription(
        "Returns one shipment by tracking number. A value that is not APX- followed by 8 digits does not match " +
        "the route and is answered with 404 as well.")]
    [ProducesResponseType<ShipmentResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ApiRoutes.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, ApiRoutes.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, ApiRoutes.ProblemJson)]
    public async Task<ActionResult<ShipmentResponse>> Get(string trackingNumber, CancellationToken cancellationToken)
    {
        var shipment = await tracking.GetAsync(trackingNumber, cancellationToken);
        return Ok(shipment.ToResponse());
    }

    /// <summary>Create a shipment</summary>
    /// <param name="cancellationToken">Cancelled when the client goes away.</param>
    /// <param name="request">The tracking number, recipient and weight.</param>
    /// <response code="201">The new shipment; the Location header points to it.</response>
    /// <response code="400">A field is missing or invalid.</response>
    /// <response code="409">A shipment with this tracking number already exists.</response>
    /// <response code="415">The body is not application/json.</response>
    /// <response code="401">No valid bearer token: missing, expired or not signed with the API's key.</response>
    /// <response code="403">The token does not have the Admin role.</response>
    /// <response code="500">An unexpected server error.</response>
    [HttpPost(ApiRoutes.VersionedShipments, Name = ApiRoutes.CreateShipmentV1)]
    [HttpPost(ApiRoutes.Shipments)]
    [Authorize(Roles = ApiRoles.Admin)]
    [EndpointSummary("Create a shipment")]
    [EndpointDescription(
        "Registers a new shipment, which starts as Created. Answers 201 with the shipment and a Location header " +
        "pointing to GET /api/v1/shipments/{trackingNumber} (on the /api/shipments alias when that was called).")]
    [Consumes(ApiRoutes.Json)]
    [ProducesResponseType<ShipmentResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, ApiRoutes.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, ApiRoutes.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status415UnsupportedMediaType, ApiRoutes.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, ApiRoutes.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, ApiRoutes.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, ApiRoutes.ProblemJson)]
    public async Task<ActionResult<ShipmentResponse>> Create(
        CreateShipmentRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var shipment = await tracking.CreateAsync(request.TrackingNumber, request.ToCommand(), cancellationToken);

        // The Location keeps the route the client called: /api/v1/... when versioned, else the Part B alias.
        return RouteData.Values.TryGetValue(ApiRoutes.VersionRouteValue, out var version)
            ? CreatedAtRoute(
                ApiRoutes.GetShipmentV1,
                new { version, trackingNumber = shipment.TrackingNumber },
                shipment.ToResponse())
            : CreatedAtAction(nameof(Get), new { trackingNumber = shipment.TrackingNumber }, shipment.ToResponse());
    }

    /// <summary>Replace a shipment</summary>
    /// <param name="trackingNumber" example="APX-20000001">APX- followed by 8 digits.</param>
    /// <param name="cancellationToken">Cancelled when the client goes away.</param>
    /// <param name="request">The new recipient and weight.</param>
    /// <response code="200">The changed shipment.</response>
    /// <response code="400">A field is missing or invalid.</response>
    /// <response code="404">No shipment has this tracking number.</response>
    /// <response code="409">The shipment is Delivered or Failed, or was changed by someone else.</response>
    /// <response code="415">The body is not application/json.</response>
    /// <response code="401">No valid bearer token: missing, expired or not signed with the API's key.</response>
    /// <response code="403">The token does not have the Admin role.</response>
    /// <response code="500">An unexpected server error.</response>
    [HttpPut(ApiRoutes.VersionedShipment, Name = ApiRoutes.ReplaceShipmentV1)]
    [HttpPut(ApiRoutes.Shipment)]
    [Authorize(Roles = ApiRoles.Admin)]
    [EndpointSummary("Replace a shipment")]
    [EndpointDescription(
        "Replaces the recipient and weight of a shipment. The tracking number and status do not change; a " +
        "Delivered or Failed shipment cannot be changed (409).")]
    [Consumes(ApiRoutes.Json)]
    [ProducesResponseType<ShipmentResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, ApiRoutes.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ApiRoutes.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, ApiRoutes.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status415UnsupportedMediaType, ApiRoutes.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, ApiRoutes.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, ApiRoutes.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, ApiRoutes.ProblemJson)]
    public async Task<ActionResult<ShipmentResponse>> Update(
        string trackingNumber,
        UpdateShipmentRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var shipment = await tracking.ReplaceAsync(trackingNumber, request.ToCommand(), cancellationToken);
        return Ok(shipment.ToResponse());
    }

    /// <summary>Change the status of a shipment</summary>
    /// <param name="trackingNumber" example="APX-20000001">APX- followed by 8 digits.</param>
    /// <param name="cancellationToken">Cancelled when the client goes away.</param>
    /// <param name="request">The status to move to.</param>
    /// <response code="200">The shipment with its new status.</response>
    /// <response code="400">The status is missing or unknown.</response>
    /// <response code="404">No shipment has this tracking number.</response>
    /// <response code="409">The move is not forward, or the shipment was changed by someone else.</response>
    /// <response code="415">The body is not application/json.</response>
    /// <response code="401">No valid bearer token: missing, expired or not signed with the API's key.</response>
    /// <response code="403">The token does not have the Courier role.</response>
    /// <response code="500">An unexpected server error.</response>
    [HttpPatch(ApiRoutes.VersionedShipment + ApiRoutes.StatusSegment, Name = ApiRoutes.ChangeShipmentStatusV1)]
    [HttpPatch(ApiRoutes.Shipment + ApiRoutes.StatusSegment)]
    [Authorize(Roles = ApiRoles.Courier)]
    [EndpointSummary("Change the status of a shipment")]
    [EndpointDescription(
        "Moves a shipment forward: Created → PickedUp → InTransit → OutForDelivery → Delivered | Failed. A move " +
        "backwards, to the same status or out of Delivered/Failed is answered with 409.")]
    [Consumes(ApiRoutes.Json)]
    [ProducesResponseType<ShipmentResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, ApiRoutes.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ApiRoutes.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, ApiRoutes.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status415UnsupportedMediaType, ApiRoutes.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, ApiRoutes.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, ApiRoutes.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, ApiRoutes.ProblemJson)]
    public async Task<ActionResult<ShipmentResponse>> UpdateStatus(
        string trackingNumber,
        UpdateStatusRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var shipment = await tracking.ChangeStatusAsync(
            trackingNumber,
            request.Status.GetValueOrDefault(),
            cancellationToken);
        return Ok(shipment.ToResponse());
    }

    /// <summary>Delete a shipment</summary>
    /// <param name="trackingNumber" example="APX-20000002">APX- followed by 8 digits.</param>
    /// <param name="cancellationToken">Cancelled when the client goes away.</param>
    /// <response code="204">The shipment was deleted.</response>
    /// <response code="404">No shipment has this tracking number.</response>
    /// <response code="409">The shipment is no longer Created.</response>
    /// <response code="401">No valid bearer token: missing, expired or not signed with the API's key.</response>
    /// <response code="403">The token does not have the Admin role.</response>
    /// <response code="500">An unexpected server error.</response>
    [HttpDelete(ApiRoutes.VersionedShipment, Name = ApiRoutes.DeleteShipmentV1)]
    [HttpDelete(ApiRoutes.Shipment)]
    [Authorize(Roles = ApiRoles.Admin)]
    [EndpointSummary("Delete a shipment")]
    [EndpointDescription(
        "Deletes a shipment that is still Created. Once a courier has picked it up the history must stay, so " +
        "any later status is answered with 409.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ApiRoutes.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, ApiRoutes.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, ApiRoutes.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, ApiRoutes.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, ApiRoutes.ProblemJson)]
    public async Task<IActionResult> Delete(string trackingNumber, CancellationToken cancellationToken)
    {
        await tracking.DeleteAsync(trackingNumber, cancellationToken);
        return NoContent();
    }
}
