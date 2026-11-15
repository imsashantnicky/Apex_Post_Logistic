using System.Net;
using System.Text;

namespace ApexPost.Courier.Presentation.Tests.Support;

/// <summary>Answers every request with one canned response and counts the requests. No network is used.</summary>
public sealed class CannedHandler(HttpStatusCode status, string? body, string mediaType) : HttpMessageHandler
{
    public List<HttpRequestMessage> Requests { get; } = [];

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        var response = new HttpResponseMessage(status);
        if (body is not null)
        {
            response.Content = new StringContent(body, Encoding.UTF8, mediaType);
        }

        return Task.FromResult(response);
    }
}
