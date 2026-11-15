using ApexPost.Application.Exceptions;
using ApexPost.Application.Tracking;
using ApexPost.Domain.Common;
using ApexPost.Domain.Tracking;
using Microsoft.AspNetCore.Mvc;

namespace ApexPost.Api.Tests.Support;

/// <summary>
/// A test-only endpoint that throws the exception named in the route, so every mapping of ApiExceptionHandler can be
/// checked before the shipment endpoints exist. It is added to the host by <see cref="ApiFactory"/> only.
/// </summary>
[ApiController]
[Route("test")]
public sealed class ThrowingController : ControllerBase
{
    public const string SecretMessage = "Internal detail that must not leak.";

    private static readonly Dictionary<string, Func<Exception>> Exceptions = new()
    {
        ["not-found"] = () => new NotFoundException("Unknown tracking number."),
        ["duplicate"] = () => new DuplicateKeyException("Shipment APX-20000001 already exists."),
        ["invalid-transition"] = () =>
            new InvalidStatusTransitionException("APX-20000008", ShipmentStatus.Delivered, ShipmentStatus.InTransit),
        ["locked"] = () => new ShipmentLockedException("Only a Created shipment can be deleted."),
        ["concurrency"] = () => new ConcurrencyConflictException("The shipment was changed by someone else."),
        ["validation"] = () => new ValidationFailedException("pageSize", "Page size must be between 1 and 50."),
        ["domain"] = () => new DomainException("Weight must be above 0 and at most 30 kg."),
        ["forbidden"] = () => new ForbiddenException("Not allowed."),
        ["unexpected"] = () => new InvalidOperationException(SecretMessage),
    };

    [HttpGet("throw/{kind}")]
    public IActionResult Throw(string kind) =>
        Exceptions.TryGetValue(kind, out var create) ? throw create() : NotFound();

    [HttpPost("echo")]
    public IActionResult Echo(EchoRequest request) => Ok(request);
}
