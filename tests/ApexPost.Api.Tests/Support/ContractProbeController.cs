using ApexPost.Api.Contracts.V1;
using Microsoft.AspNetCore.Mvc;

namespace ApexPost.Api.Tests.Support;

/// <summary>
/// A test-only endpoint that binds the v1 request contracts, so the automatic [ApiController] 400 can be checked
/// before the shipment endpoints exist. It has no ModelState check of its own. Added by <see cref="ApiFactory"/>.
/// </summary>
[ApiController]
[Route("test/contracts")]
public sealed class ContractProbeController : ControllerBase
{
    [HttpPost("shipments")]
    public IActionResult Create(CreateShipmentRequest request) => Ok(request.ToCommand());

    [HttpPost("scans")]
    public IActionResult Upload(ScanUploadRequest request) => Ok(request.ToUploads().Count);

    [HttpPatch("status")]
    public IActionResult ChangeStatus(UpdateStatusRequest request) => Ok(request.Status);
}
