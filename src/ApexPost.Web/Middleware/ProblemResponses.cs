using System.Diagnostics;
using ApexPost.Application.Exceptions;
using ApexPost.Domain.Common;
using ApexPost.Web.Composition;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;

namespace ApexPost.Web.Middleware;

/// <summary>
/// Builds every RFC 7807 answer of the platform. JSON callers receive it as application/problem+json; a browser
/// gets the same ProblemDetails rendered by the error page. Details here are safe to show to anyone.
/// </summary>
public static class ProblemResponses
{
    public const string TraceIdKey = "traceId";
    public const string CorrelationIdKey = "correlationId";
    public const string StackTraceKey = "stackTrace";
    public const int ClientClosedRequest = 499;

    private const string ContentType = "application/problem+json";
    private const string HtmlMediaType = "text/html";
    private const string ProblemItem = "ApexPost.Problem";

    public static ProblemDetails Create(HttpContext context, int status, string? detail = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        return Stamp(context, new ProblemDetails
        {
            Title = TitleFor(status),
            Status = status,
            Detail = detail ?? DetailFor(status),
        });
    }

    public static ValidationProblemDetails CreateValidation(HttpContext context, ModelStateDictionary modelState)
    {
        ArgumentNullException.ThrowIfNull(context);
        return Stamp(context, new ValidationProblemDetails(modelState)
        {
            Title = TitleFor(StatusCodes.Status400BadRequest),
            Status = StatusCodes.Status400BadRequest,
            Detail = DetailFor(StatusCodes.Status400BadRequest),
        });
    }

    public static int StatusFor(Exception exception) => exception switch
    {
        NotFoundException => StatusCodes.Status404NotFound,
        ForbiddenException => StatusCodes.Status403Forbidden,
        ValidationFailedException or DomainException => StatusCodes.Status400BadRequest,
        ConcurrencyException => StatusCodes.Status409Conflict,
        ExternalServiceTimeoutException => StatusCodes.Status504GatewayTimeout,
        ExternalServiceException => StatusCodes.Status502BadGateway,
        BadHttpRequestException badRequest => badRequest.StatusCode,
        _ => StatusCodes.Status500InternalServerError,
    };

    /// <summary>Partners always get JSON, whatever their Accept header says.</summary>
    public static bool WantsHtml(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return !ApexRoutes.IsIntegration(context)
            && context.Request.Headers[HeaderNames.Accept].ToString()
                .Contains(HtmlMediaType, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Stops a request from middleware. Outside /integration the status alone is enough: the status page answers
    /// browsers and JSON callers alike, and that is where a denied request is audited.
    /// </summary>
    public static async Task WriteAsync(HttpContext context, int status)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Response.StatusCode = status;
        if (ApexRoutes.IsIntegration(context))
        {
            await WriteJsonAsync(context, Create(context, status));
        }
    }

    public static Task WriteJsonAsync(HttpContext context, ProblemDetails problem)
    {
        ArgumentNullException.ThrowIfNull(context);
        return Results.Problem(problem).ExecuteAsync(context);
    }

    public static ObjectResult ToActionResult(ProblemDetails problem)
    {
        ArgumentNullException.ThrowIfNull(problem);
        return new ObjectResult(problem) { StatusCode = problem.Status, ContentTypes = { ContentType } };
    }

    /// <summary>Hands a problem from the exception handler to the re-executed error page.</summary>
    public static void Remember(HttpContext context, ProblemDetails problem)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Items[ProblemItem] = problem;
    }

    public static ProblemDetails? Recall(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.Items[ProblemItem] as ProblemDetails;
    }

    private static TProblem Stamp<TProblem>(HttpContext context, TProblem problem)
        where TProblem : ProblemDetails
    {
        problem.Extensions[TraceIdKey] = Activity.Current?.Id ?? context.TraceIdentifier;
        problem.Extensions[CorrelationIdKey] = context.TraceIdentifier;
        return problem;
    }

    private static string TitleFor(int status) => status switch
    {
        StatusCodes.Status403Forbidden => "Access denied",
        ClientClosedRequest => "Client closed the request",
        _ => ReasonPhrases.GetReasonPhrase(status),
    };

    private static string DetailFor(int status) => status switch
    {
        StatusCodes.Status400BadRequest => "The request could not be processed.",
        StatusCodes.Status401Unauthorized => "A valid API key is required.",
        StatusCodes.Status403Forbidden => "You do not have access to this resource.",
        StatusCodes.Status404NotFound => "The page or record does not exist.",
        StatusCodes.Status409Conflict => "This record was changed by someone else. Check it and try again.",
        StatusCodes.Status429TooManyRequests => "Too many requests. Wait a moment and try again.",
        StatusCodes.Status502BadGateway => "A partner service failed. Try again later.",
        StatusCodes.Status503ServiceUnavailable => "The service is down for maintenance. Try again later.",
        StatusCodes.Status504GatewayTimeout => "A partner service did not answer in time. Try again later.",
        _ => "Something went wrong on our side. Try again later.",
    };
}
