using System.Net;
using System.Text;

namespace ApexPost.Courier.ApiClient.Tests.Support;

/// <summary>Answers every request with a canned response and records what was sent. No network is used.</summary>
public sealed class FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
{
    public List<RecordedRequest> Requests { get; } = [];

    public static FakeHttpMessageHandler Returning(HttpStatusCode status, string? body, string mediaType) =>
        new(_ => Response(status, body, mediaType));

    public static FakeHttpMessageHandler Throwing(Exception exception) => new(_ => throw exception);

    public static HttpResponseMessage Response(HttpStatusCode status, string? body, string mediaType)
    {
        var response = new HttpResponseMessage(status);
        if (body is not null)
        {
            response.Content = new StringContent(body, Encoding.UTF8, mediaType);
        }

        return response;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        Requests.Add(new RecordedRequest(
            request.Method,
            request.RequestUri!,
            request.Headers.Authorization?.ToString(),
            request.Content?.Headers.ContentType?.MediaType,
            body));
        return respond(request);
    }
}
