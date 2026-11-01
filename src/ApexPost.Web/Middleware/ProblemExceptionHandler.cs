using ApexPost.Application.Exceptions;
using ApexPost.Domain.Common;
using Microsoft.AspNetCore.Diagnostics;

namespace ApexPost.Web.Middleware;

/// <summary>
/// Maps an exception to a status and a safe ProblemDetails. JSON callers are answered here; returning false lets
/// UseExceptionHandler re-execute the error page for browsers. A client that hangs up never gets here: the
/// built-in middleware answers 499 without logging an error.
/// </summary>
public sealed partial class ProblemExceptionHandler(
    ILogger<ProblemExceptionHandler> logger,
    IHostEnvironment environment) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(exception);
        var status = ProblemResponses.StatusFor(exception);
        Log(exception, status);

        var problem = ProblemResponses.Create(httpContext, status, SafeDetail(exception));
        if (environment.IsDevelopment())
        {
            problem.Extensions[ProblemResponses.StackTraceKey] = exception.ToString();
        }

        httpContext.Response.StatusCode = status;
        ProblemResponses.Remember(httpContext, problem);
        if (ProblemResponses.WantsHtml(httpContext))
        {
            return false;
        }

        await ProblemResponses.WriteJsonAsync(httpContext, problem);
        return true;
    }

    /// <summary>Only rule messages are written for users; any other message stays in the log.</summary>
    private static string? SafeDetail(Exception exception) =>
        exception is ValidationFailedException or DomainException ? exception.Message : null;

    private void Log(Exception exception, int status)
    {
        if (status >= StatusCodes.Status500InternalServerError)
        {
            LogFailure(exception, status);
        }
        else
        {
            var exceptionType = exception.GetType().Name;
            LogRejected(exceptionType, status);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "The request failed with status {Status}")]
    private partial void LogFailure(Exception exception, int status);

    [LoggerMessage(Level = LogLevel.Information, Message = "{ExceptionType} answered with status {Status}")]
    private partial void LogRejected(string exceptionType, int status);
}
