using System.ComponentModel.DataAnnotations;
using ApexPost.Api.Contracts.V1;
using ApexPost.Application.Tracking;
using ApexPost.Domain.Tracking;
using Microsoft.AspNetCore.Mvc;

namespace ApexPost.Api.Controllers;

/// <summary>
/// Shipments: list, read, create, replace, change status and delete. Each action binds the input, calls
/// <see cref="IShipmentTrackingService"/> and returns the result; errors are thrown by the service and written as
/// ProblemDetails by the global exception handler.
/// </summary>
[ApiController]
[Route(ApiRoutes.Shipments)]
[ProducesJson]
public sealed class ShipmentsController(IShipmentTrackingService tracking) : ControllerBase
{
    /// <summary>One page of shipments, optionally of one status and one city.</summary>
    /// <param name="page">The page number, 1 or more.</param>
    /// <param name="pageSize">Shipments per page, 1 to 50 (default 20).</param>
    /// <param name="status">Only shipments with this status (name or number).</param>
    /// <param name="city">Only shipments to this city, ignoring case.</param>
    /// <param name="cancellationToken">Cancelled when the client goes away.</param>
    [HttpGet(Name = ApiRoutes.ListShipments)]
    [ProducesResponseType<PagedResponse<ShipmentResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
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

    /// <summary>One shipment by tracking number.</summary>
    /// <param name="trackingNumber">APX- followed by 8 digits.</param>
    /// <param name="cancellationToken">Cancelled when the client goes away.</param>
    [HttpGet(ApiRoutes.TrackingNumber, Name = ApiRoutes.GetShipment)]
    [ProducesResponseType<ShipmentResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ShipmentResponse>> Get(string trackingNumber, CancellationToken cancellationToken)
    {
        var shipment = await tracking.GetAsync(trackingNumber, cancellationToken);
        return Ok(shipment.ToResponse());
    }

    /// <summary>Registers a new shipment, which starts as Created.</summary>
    /// <param name="request">The tracking number, recipient and weight.</param>
    /// <param name="cancellationToken">Cancelled when the client goes away.</param>
    [HttpPost(Name = ApiRoutes.CreateShipment)]
    [Consumes(ApiRoutes.Json)]
    [ProducesResponseType<ShipmentResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status415UnsupportedMediaType)]
    public async Task<ActionResult<ShipmentResponse>> Create(
        CreateShipmentRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var shipment = await tracking.CreateAsync(request.TrackingNumber, request.ToCommand(), cancellationToken);
        return CreatedAtAction(nameof(Get), new { trackingNumber = shipment.TrackingNumber }, shipment.ToResponse());
    }

    /// <summary>Replaces the recipient and weight of a shipment that is not Delivered or Failed.</summary>
    /// <param name="trackingNumber">APX- followed by 8 digits.</param>
    /// <param name="request">The new recipient and weight.</param>
    /// <param name="cancellationToken">Cancelled when the client goes away.</param>
    [HttpPut(ApiRoutes.TrackingNumber, Name = ApiRoutes.UpdateShipment)]
    [Consumes(ApiRoutes.Json)]
    [ProducesResponseType<ShipmentResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status415UnsupportedMediaType)]
    public async Task<ActionResult<ShipmentResponse>> Update(
        string trackingNumber,
        UpdateShipmentRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var shipment = await tracking.ReplaceAsync(trackingNumber, request.ToCommand(), cancellationToken);
        return Ok(shipment.ToResponse());
    }

    /// <summary>Moves a shipment forward in its lifecycle.</summary>
    /// <param name="trackingNumber">APX- followed by 8 digits.</param>
    /// <param name="request">The status to move to.</param>
    /// <param name="cancellationToken">Cancelled when the client goes away.</param>
    [HttpPatch(ApiRoutes.TrackingNumber + "/status", Name = ApiRoutes.UpdateShipmentStatus)]
    [Consumes(ApiRoutes.Json)]
    [ProducesResponseType<ShipmentResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status415UnsupportedMediaType)]
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

    /// <summary>Deletes a shipment that is still Created.</summary>
    /// <param name="trackingNumber">APX- followed by 8 digits.</param>
    /// <param name="cancellationToken">Cancelled when the client goes away.</param>
    [HttpDelete(ApiRoutes.TrackingNumber, Name = ApiRoutes.DeleteShipment)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(string trackingNumber, CancellationToken cancellationToken)
    {
        await tracking.DeleteAsync(trackingNumber, cancellationToken);
        return NoContent();
    }
}
