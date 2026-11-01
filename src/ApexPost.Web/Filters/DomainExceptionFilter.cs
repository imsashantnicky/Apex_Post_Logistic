using ApexPost.Application.Exceptions;
using ApexPost.Domain.Common;
using ApexPost.Web.Controllers;
using ApexPost.Web.Middleware;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ApexPost.Web.Filters;

/// <summary>
/// Turns the exceptions of the services into action results. A failed form POST follows Post/Redirect/Get: the
/// message goes into a flash and the browser returns to the form. It is a filter because only the controller knows
/// that form. It is an action filter rather than an exception filter, because those do not get the controller.
/// Anything not handled here, such as a caller that asked for JSON, goes on to the exception handler.
/// </summary>
public sealed class DomainExceptionFilter : IAsyncActionFilter
{
    private const string ConflictMessage = "This record was changed by someone else. Check it and try again.";
    private const string DuplicateMessage = "A record with these details already exists.";
    private const string InUseMessage = "This record has history and cannot be removed.";

    private static readonly Dictionary<string, string[]> NoFieldErrors = [];

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        ArgumentNullException.ThrowIfNull(next);
        var executed = await next();
        if (executed is { Exception: { } exception, ExceptionHandled: false, Controller: ApexController page }
            && ResultFor(executed.HttpContext, page, exception) is { } result)
        {
            executed.Result = result;
            executed.ExceptionHandled = true;
        }
    }

    private static IActionResult? ResultFor(HttpContext http, ApexController page, Exception exception)
    {
        switch (exception)
        {
            case ForbiddenException:
                return new ForbidResult();
            case NotFoundException:
                return new NotFoundResult();
        }

        if (!HttpMethods.IsPost(http.Request.Method)
            || ProblemResponses.WantsJson(http)
            || FormMessage(exception) is not { } message)
        {
            return null;
        }

        AuditOutcome.Set(http, ProblemResponses.StatusFor(exception));
        page.FlashError(message);
        if (exception is not ConcurrencyException && http.Request.HasFormContentType)
        {
            page.RememberForm((exception as ValidationFailedException)?.Errors ?? NoFieldErrors);
        }

        return page.AfterFormError();
    }

    private static string? FormMessage(Exception exception) => exception switch
    {
        ValidationFailedException { Errors.Count: > 0 } invalid =>
            string.Join(' ', invalid.Errors.Values.SelectMany(messages => messages)),
        ValidationFailedException or DomainException => exception.Message,
        ConcurrencyException => ConflictMessage,
        DuplicateKeyException => DuplicateMessage,
        RecordInUseException => InUseMessage,
        _ => null,
    };
}
