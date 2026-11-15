using ApexPost.Api.Contracts.V2;
using ApexPost.Api.Security;
using ApexPost.Application.Tracking;
using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;

namespace ApexPost.Api.Controllers.V2;

/// <summary>
/// Read-only tracking for partners (v2 only). A partner sends its API key in X-Api-Key instead of a user token;
/// <see cref="PartnerApiKeyAttribute"/> checks it before the action runs. Same service as the courier endpoints.
/// </summary>
[ApiController]
[ApiVersion(ApiRoutes.Version2)]
[Route(ApiRoutes.VersionedPartnerShipments)]
[Tags(ApiRoutes.PartnerTag)]
[PartnerApiKey]
[ProducesJson]
public sealed class PartnerShipmentsController(IShipmentTrackingService tracking) : ControllerBase
{
    /// <summary>Track a shipment as a partner</summary>
    /// <param name="trackingNumber" example="APX-20000005">APX- followed by 8 digits.</param>
    /// <param name="cancellationToken">Cancelled when the client goes away.</param>
    /// <response code="200">Status, destination city and latest scan of the shipment.</response>
    /// <response code="401">The X-Api-Key header is missing or holds an unknown key.</response>
    /// <response code="404">No shipment has this tracking number, or it is not APX- followed by 8 digits.</response>
    /// <response code="500">An unexpected server error.</response>
    [HttpGet(ApiRoutes.TrackingNumber, Name = ApiRoutes.GetPartnerShipment)]
    [EndpointSummary("Track a shipment as a partner")]
    [EndpointDescription(
        "Returns the status, destination city and latest scan of one shipment, without the recipient's name, " +
        "phone or address. Authenticate with a partner key in the X-Api-Key header (security scheme ApiKey).")]
    [ProducesResponseType<PartnerShipmentResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, ApiRoutes.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ApiRoutes.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, ApiRoutes.ProblemJson)]
    public async Task<ActionResult<PartnerShipmentResponse>> Get(
        string trackingNumber,
        CancellationToken cancellationToken)
    {
        var shipment = await tracking.GetAsync(trackingNumber, cancellationToken);
        return Ok(shipment.ToPartnerResponse());
    }
}
