using ApexPost.Application.Exceptions;
using ApexPost.Application.Tracking;
using ApexPost.Domain.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace ApexPost.Api.Errors;

/// <summary>
/// The one place where an exception becomes an HTTP answer. Every answer is ProblemDetails with the trace id:
/// NotFound 404; duplicate key, invalid status transition, locked shipment (delete after Created) and concurrency
/// conflict 409; validation and domain rule 400 (validation with the errors per field); forbidden 403; everything
/// else 500 with a generic detail. The stack trace is added only in Development.
/// </summary>
public sealed partial class ApiExceptionHandler(
    IProblemDetailsService problems,
    IHostEnvironment environment,
    ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    /// <summary>The status nginx made common for a client that closed the request; nothing is sent back.</summary>
    public const int ClientClosedRequest = 499;

    /// <summary>The ProblemDetails extension with the full exception, written in Development only.</summary>
    public const string StackTraceKey = "stackTrace";

    /// <summary>The detail of a 500, which never shows the exception message.</summary>
    public const string UnexpectedDetail = "An unexpected error occurred. Quote the trace id when reporting it.";

    /// <summary>Writes the exception as ProblemDetails; a request the client cancelled gets 499, no body.</summary>
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(exception);

        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            LogCancelled(logger, httpContext.Request.Method, httpContext.Request.Path);
            httpContext.Response.StatusCode = ClientClosedRequest;
            return true;
        }

        var (status, title) = Classify(exception);
        LogFailure(status, httpContext, exception);

        var problem = exception is ValidationFailedException validation
            ? new ValidationProblemDetails(validation.Errors.ToDictionary())
            : new ProblemDetails();
        problem.Status = status;
        problem.Title = title;
        problem.Detail = status == StatusCodes.Status500InternalServerError ? UnexpectedDetail : exception.Message;
        problem.Instance = httpContext.Request.Path;
        if (environment.IsDevelopment())
        {
            problem.Extensions[StackTraceKey] = exception.ToString();
        }

        httpContext.Response.StatusCode = status;
        return await problems.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception,
        }).ConfigureAwait(false);
    }

    private static (int Status, string Title) Classify(Exception exception) => exception switch
    {
        NotFoundException => (StatusCodes.Status404NotFound, "Not found"),
        DuplicateKeyException => (StatusCodes.Status409Conflict, "Duplicate"),
        InvalidStatusTransitionException => (StatusCodes.Status409Conflict, "Invalid status transition"),
        ShipmentLockedException => (StatusCodes.Status409Conflict, "Shipment cannot change"),
        ConcurrencyException => (StatusCodes.Status409Conflict, "Changed by someone else"),
        ValidationFailedException => (StatusCodes.Status400BadRequest, "Validation failed"),
        DomainException => (StatusCodes.Status400BadRequest, "Business rule violated"),
        ForbiddenException => (StatusCodes.Status403Forbidden, "Forbidden"),
        BadHttpRequestException bad => (bad.StatusCode, "Bad request"),
        _ => (StatusCodes.Status500InternalServerError, "Server error"),
    };

    private void LogFailure(int status, HttpContext context, Exception exception)
    {
        if (status >= StatusCodes.Status500InternalServerError)
        {
            LogUnexpected(logger, exception, context.Request.Method, context.Request.Path);
        }
        else
        {
            var exceptionType = exception.GetType().Name;
            LogRejected(logger, context.Request.Method, context.Request.Path, status, exceptionType);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled exception for {Method} {Path}.")]
    private static partial void LogUnexpected(ILogger logger, Exception exception, string method, PathString path);

    [LoggerMessage(Level = LogLevel.Information, Message = "{Method} {Path} answered {Status} for {ExceptionType}.")]
    private static partial void LogRejected(
        ILogger logger,
        string method,
        PathString path,
        int status,
        string exceptionType);

    [LoggerMessage(Level = LogLevel.Information, Message = "{Method} {Path} was cancelled by the client.")]
    private static partial void LogCancelled(ILogger logger, string method, PathString path);
}
