using ApexPost.Web.Middleware;
using Microsoft.AspNetCore.Mvc;

namespace ApexPost.Web.ViewModels;

/// <summary>A ProblemDetails as the error page shows it.</summary>
public sealed record StatusPage(
    int? Status,
    string? Title,
    string? Detail,
    string? TraceId,
    string? CorrelationId,
    string? Stack)
{
    public static StatusPage From(ProblemDetails problem)
    {
        ArgumentNullException.ThrowIfNull(problem);
        return new StatusPage(
            problem.Status,
            problem.Title,
            problem.Detail,
            Text(problem, ProblemResponses.TraceIdKey),
            Text(problem, ProblemResponses.CorrelationIdKey),
            Text(problem, ProblemResponses.StackTraceKey));
    }

    private static string? Text(ProblemDetails problem, string key) =>
        problem.Extensions.TryGetValue(key, out var value) ? value as string : null;
}
