using System.Diagnostics;
using System.Globalization;
using ApexPost.Application.Abstractions;
using ApexPost.Application.DTOs;
using ApexPost.Application.Extensions;
using ApexPost.Application.Services;
using ApexPost.Web.Middleware;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Routing;

namespace ApexPost.Web.Filters;

/// <summary>
/// Records every action marked <see cref="AuditAttribute"/>: who, what, the outcome, the duration and the correlation
/// ID. It is a filter because only MVC knows the action, its bound arguments and its result. It stores route values
/// and scalar arguments, never a bound form model, and everything passes through <see cref="AuditRedactor"/>.
/// On the re-executed error page the entry describes the denied request, not the page.
/// </summary>
public sealed class AuditActionFilter(IAuditStore store, IClock clock) : IAsyncActionFilter
{
    private const string ControllerKey = "controller";
    private const string ActionKey = "action";

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);
        var marker = context.ActionDescriptor.EndpointMetadata.OfType<AuditAttribute>().FirstOrDefault();
        if (marker is null)
        {
            await next();
            return;
        }

        var arguments = ScalarArguments(context.ActionArguments);
        var started = Stopwatch.GetTimestamp();
        var executed = await next();

        var status = StatusOf(executed);
        if (marker.OnlyForStatus == 0 || marker.OnlyForStatus == status)
        {
            var duration = (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            // The action has already happened: a client hanging up now must not lose its audit entry.
            await store.AppendAsync(
                Entry(context, marker.EventName, arguments, status, duration),
                CancellationToken.None);
        }
    }

    private AuditEntry Entry(
        ActionExecutingContext context,
        string eventName,
        List<KeyValuePair<string, string>> arguments,
        int status,
        long durationMs)
    {
        var http = context.HttpContext;
        var denied = http.Features.Get<IStatusCodeReExecuteFeature>()?.RouteValues;
        var target = denied ?? context.RouteData.Values;
        return new AuditEntry(
            clock.UtcNow,
            eventName,
            http.User.StaffId,
            http.User.Role,
            http.User.BranchCode,
            Text(target[ControllerKey]),
            Text(target[ActionKey]),
            AuditRedactor.Redact(KeyValues(target, denied is null ? arguments : [])),
            status,
            durationMs,
            http.TraceIdentifier);
    }

    private static IEnumerable<KeyValuePair<string, string>> KeyValues(
        RouteValueDictionary route,
        List<KeyValuePair<string, string>> arguments) =>
        route
            .Where(pair => pair.Key is not (ControllerKey or ActionKey))
            .Select(pair => KeyValuePair.Create(pair.Key, Text(pair.Value)))
            .Concat(arguments)
            .Where(pair => pair.Value.Length > 0)
            .GroupBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.Last());

    /// <summary>Only simple values: a bound model may hold a password or a document number.</summary>
    private static List<KeyValuePair<string, string>> ScalarArguments(IDictionary<string, object?> arguments) =>
        [
            .. arguments
                .Where(pair => pair.Value is string or Guid or Enum or int or decimal or bool)
                .Select(pair => KeyValuePair.Create(pair.Key, Text(pair.Value))),
        ];

    private static string Text(object? value) => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;

    /// <summary>An action filter runs before the result does, so the status is read from the result itself.</summary>
    private static int StatusOf(ActionExecutedContext executed)
    {
        if (AuditOutcome.Of(executed.HttpContext) is { } outcome)
        {
            return outcome;
        }

        if (executed.Exception is { } exception && !executed.ExceptionHandled)
        {
            return ProblemResponses.StatusFor(exception);
        }

        return executed.Result switch
        {
            IStatusCodeActionResult { StatusCode: { } status } => status,
            ForbidResult => StatusCodes.Status403Forbidden,
            ChallengeResult => StatusCodes.Status401Unauthorized,
            RedirectToActionResult or RedirectResult or LocalRedirectResult => StatusCodes.Status302Found,
            _ => executed.HttpContext.Response.StatusCode,
        };
    }
}
