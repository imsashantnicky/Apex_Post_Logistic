namespace ApexPost.Web.Middleware;

/// <summary>
/// Adds the browser protection headers just before the response starts, so error responses get them too,
/// even after the exception handler cleared the headers.
/// </summary>
public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    private const string ContentSecurityPolicy =
        "default-src 'self'; object-src 'none'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'";

    private const string NoSniff = "nosniff";
    private const string Deny = "DENY";
    private const string ReferrerPolicy = "strict-origin-when-cross-origin";

    public Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;
            headers.ContentSecurityPolicy = ContentSecurityPolicy;
            headers.XContentTypeOptions = NoSniff;
            headers.XFrameOptions = Deny;
            headers["Referrer-Policy"] = ReferrerPolicy;
            return Task.CompletedTask;
        });
        return next(context);
    }
}
