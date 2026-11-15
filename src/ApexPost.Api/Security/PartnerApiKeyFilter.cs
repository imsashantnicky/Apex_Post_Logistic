using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace ApexPost.Api.Security;

/// <summary>
/// Lets a request through only with a known partner key in the <c>X-Api-Key</c> header; otherwise the action does
/// not run and the answer is 401 ProblemDetails with <c>WWW-Authenticate: ApiKey</c>. Only the partner name is
/// logged, never the key.
/// </summary>
public sealed partial class PartnerApiKeyFilter(
    PartnerKeyStore keys,
    ProblemDetailsFactory problems,
    ILogger<PartnerApiKeyFilter> logger) : IAuthorizationFilter
{
    /// <summary>The request header that carries the key.</summary>
    public const string HeaderName = "X-Api-Key";

    /// <summary>The scheme named in WWW-Authenticate and in the OpenAPI document.</summary>
    public const string SchemeName = "ApiKey";

    /// <summary>The HttpContext.Items key under which the matched partner name is stored.</summary>
    public const string PartnerItemKey = "ApexPost.Partner";

    /// <summary>The detail of a 401 without the header.</summary>
    public const string MissingKeyDetail = "An API key is required. Send it in the X-Api-Key header.";

    /// <summary>The detail of a 401 with an unknown key.</summary>
    public const string InvalidKeyDetail = "The API key is not valid.";

    /// <summary>Checks the header before the action runs.</summary>
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var http = context.HttpContext;
        var presented = http.Request.Headers[HeaderName].ToString();
        if (keys.FindPartner(presented) is { } partner)
        {
            http.Items[PartnerItemKey] = partner;
            LogAccepted(logger, partner, http.Request.Path);
            return;
        }

        var missing = string.IsNullOrEmpty(presented);
        LogRefused(logger, http.Request.Path, missing ? "missing" : "invalid");
        var problem = problems.CreateProblemDetails(
            http,
            StatusCodes.Status401Unauthorized,
            "Unauthorized",
            detail: missing ? MissingKeyDetail : InvalidKeyDetail,
            instance: http.Request.Path);
        http.Response.Headers.WWWAuthenticate = SchemeName;
        context.Result = new ObjectResult(problem)
        {
            StatusCode = StatusCodes.Status401Unauthorized,
            ContentTypes = { "application/problem+json" },
        };
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Partner {Partner} was let in to {Path}.")]
    private static partial void LogAccepted(ILogger logger, string partner, PathString path);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Partner request to {Path} refused: the API key is {Reason}.")]
    private static partial void LogRefused(ILogger logger, PathString path, string reason);
}
