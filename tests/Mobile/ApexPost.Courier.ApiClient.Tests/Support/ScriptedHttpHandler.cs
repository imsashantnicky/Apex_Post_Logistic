using System.Collections.Concurrent;
using System.Net;

namespace ApexPost.Courier.ApiClient.Tests.Support;

/// <summary>
/// Primary handler for the resilience tests: answers attempt N (1-based) with the scripted response and records
/// the body and the Authorization header of every attempt. No network is used.
/// </summary>
public sealed class ScriptedHttpHandler(Func<int, CancellationToken, Task<HttpResponseMessage>> respond)
    : HttpMessageHandler
{
    private int _attempts;

    public int Attempts => Volatile.Read(ref _attempts);

    public ConcurrentQueue<string?> Bodies { get; } = new();

    public ConcurrentQueue<string?> Authorizations { get; } = new();

    /// <summary>Released once each time an attempt reaches the handler, so a test can wait for it.</summary>
    public SemaphoreSlim Entered { get; } = new(0);

    /// <summary>Every attempt hangs until its token is cancelled (attempt timeout or caller).</summary>
    public static ScriptedHttpHandler Hanging() =>
        new(async (_, ct) =>
        {
            await Task.Delay(Timeout.Infinite, ct);
            throw new InvalidOperationException("Unreachable: an infinite delay only ends by cancellation.");
        });

    /// <summary>Every attempt gets <paramref name="status"/> with <paramref name="body"/> as ProblemDetails.</summary>
    public static ScriptedHttpHandler Problem(HttpStatusCode status, string body) =>
        new((_, _) => Task.FromResult(FakeHttpMessageHandler.Response(status, body, SampleJson.ProblemJson)));

    /// <summary>Answers with the statuses in order; the last one repeats. 200 answers carry <paramref name="okBody"/>.</summary>
    public static ScriptedHttpHandler Statuses(string okBody, params HttpStatusCode[] statuses) =>
        new((attempt, _) =>
        {
            var status = statuses[Math.Min(attempt, statuses.Length) - 1];
            var body = status == HttpStatusCode.OK ? okBody : null;
            return Task.FromResult(FakeHttpMessageHandler.Response(status, body, SampleJson.Json));
        });

    /// <summary>Throws <paramref name="exception"/> for the first <paramref name="failures"/> attempts, then 200.</summary>
    public static ScriptedHttpHandler ThrowingFirst(int failures, Exception exception, string okBody) =>
        new((attempt, _) => attempt <= failures
            ? Task.FromException<HttpResponseMessage>(exception)
            : Task.FromResult(FakeHttpMessageHandler.Response(HttpStatusCode.OK, okBody, SampleJson.Json)));

    /// <summary>Hangs (until the attempt is cancelled) for the first <paramref name="hanging"/> attempts, then 200.</summary>
    public static ScriptedHttpHandler HangingFirst(int hanging, string okBody) =>
        new(async (attempt, ct) =>
        {
            if (attempt <= hanging)
            {
                await Task.Delay(Timeout.Infinite, ct);
            }

            return FakeHttpMessageHandler.Response(HttpStatusCode.OK, okBody, SampleJson.Json);
        });

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var attempt = Interlocked.Increment(ref _attempts);
        Authorizations.Enqueue(request.Headers.Authorization?.ToString());
        Bodies.Enqueue(request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken));
        Entered.Release();
        return await respond(attempt, cancellationToken);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Entered.Dispose();
        }

        base.Dispose(disposing);
    }
}
