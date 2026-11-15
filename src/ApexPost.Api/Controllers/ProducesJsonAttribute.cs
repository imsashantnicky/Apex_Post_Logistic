using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ApexPost.Api.Controllers;

/// <summary>
/// <c>[Produces("application/json")]</c> for successful answers. The plain attribute also forces application/json
/// onto the automatic 400 ValidationProblemDetails; this one leaves ProblemDetails results alone, so every error
/// keeps application/problem+json as RFC 9457 asks.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class ProducesJsonAttribute : ProducesAttribute
{
    /// <summary>Declares application/json as the response type.</summary>
    public ProducesJsonAttribute()
        : base(ApiRoutes.Json)
    {
    }

    /// <inheritdoc />
    public override void OnResultExecuting(ResultExecutingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.Result is ObjectResult { Value: ProblemDetails })
        {
            return;
        }

        base.OnResultExecuting(context);
    }
}
