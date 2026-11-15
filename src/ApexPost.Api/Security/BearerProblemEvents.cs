using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace ApexPost.Api.Security;

/// <summary>
/// Turns the empty 401 and 403 of the JWT bearer handler into ProblemDetails, like every other error of the API.
/// The detail says why a token was refused (missing, expired, invalid) but never echoes the token itself.
/// </summary>
public sealed class BearerProblemEvents : JwtBearerEvents
{
    /// <summary>The detail of a 401 without a token.</summary>
    public const string MissingTokenDetail = "A bearer token is required. Send Authorization: Bearer <token>.";

    /// <summary>The detail of a 401 for an expired token.</summary>
    public const string ExpiredTokenDetail = "The bearer token has expired. Sign in again for a new token.";

    /// <summary>The detail of a 401 for a token that failed any other check (signature, issuer, audience).</summary>
    public const string InvalidTokenDetail = "The bearer token is not valid.";

    /// <summary>The detail of a 403 from a role rule.</summary>
    public const string ForbiddenDetail = "The role of your token does not allow this action.";

    /// <summary>Writes 401 ProblemDetails with a WWW-Authenticate header instead of the empty default answer.</summary>
    public override async Task Challenge(JwtBearerChallengeContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.HandleResponse();
        if (context.Response.HasStarted)
        {
            return;
        }

        var detail = context.AuthenticateFailure switch
        {
            null => MissingTokenDetail,
            SecurityTokenExpiredException => ExpiredTokenDetail,
            _ => InvalidTokenDetail,
        };
        context.Response.Headers.WWWAuthenticate = context.AuthenticateFailure is null
            ? JwtBearerDefaults.AuthenticationScheme
            : JwtBearerDefaults.AuthenticationScheme + " error=\"invalid_token\"";
        await WriteAsync(context.HttpContext, StatusCodes.Status401Unauthorized, "Unauthorized", detail)
            .ConfigureAwait(false);
    }

    /// <summary>Writes 403 ProblemDetails when the token is valid but its role does not match the action.</summary>
    public override async Task Forbidden(ForbiddenContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.Response.HasStarted)
        {
            return;
        }

        await WriteAsync(context.HttpContext, StatusCodes.Status403Forbidden, "Forbidden", ForbiddenDetail)
            .ConfigureAwait(false);
    }

    private static async Task WriteAsync(HttpContext http, int status, string title, string detail)
    {
        http.Response.StatusCode = status;
        var problems = http.RequestServices.GetRequiredService<IProblemDetailsService>();
        await problems.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = http,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = title,
                Detail = detail,
                Instance = http.Request.Path,
            },
        }).ConfigureAwait(false);
    }
}
