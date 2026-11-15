namespace ApexPost.Courier.ApiClient.Tests.Support;

/// <summary>
/// Sits directly in front of every fake handler and fails the test when a request could leave the process: the
/// host must be <see cref="FakeHost"/> (the reserved <c>.test</c> domain never resolves) and no handler in the chain
/// below may be a real <see cref="HttpClientHandler"/> or <see cref="SocketsHttpHandler"/>.
/// </summary>
public sealed class NetworkGuardHandler(HttpMessageHandler fake) : DelegatingHandler(fake)
{
    /// <summary>The only host a test request may name.</summary>
    public const string FakeHost = "apexpost.test";

    /// <summary>Base address of every test client.</summary>
    public static readonly Uri BaseAddress = new($"https://{FakeHost}/");

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var host = request.RequestUri?.Host;
        if (!string.Equals(host, FakeHost, StringComparison.OrdinalIgnoreCase))
        {
            Assert.Fail($"Network guard: a request to '{host}' was blocked; tests may only call {FakeHost}.");
        }

        if (ReachesRealNetwork(InnerHandler))
        {
            Assert.Fail("Network guard: the request would reach a real network handler, not a fake.");
        }

        return base.SendAsync(request, cancellationToken);
    }

    private static bool ReachesRealNetwork(HttpMessageHandler? handler)
    {
        while (handler is DelegatingHandler delegating)
        {
            handler = delegating.InnerHandler;
        }

        return handler is null or HttpClientHandler or SocketsHttpHandler;
    }
}
