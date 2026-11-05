using System.ComponentModel.DataAnnotations;

namespace ApexPost.Api.Tests.Support;

/// <summary>A body with one required field, to see the [ApiController] model validation answer.</summary>
public sealed record EchoRequest([Required] string? Name);
