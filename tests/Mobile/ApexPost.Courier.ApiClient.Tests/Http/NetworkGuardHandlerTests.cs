using System.Net;
using ApexPost.Courier.ApiClient.Tests.Support;
using Xunit.Sdk;

namespace ApexPost.Courier.ApiClient.Tests.Http;

/// <summary>Proves the guard every client test runs behind: no request can leave the process.</summary>
[Trait("Category", "Unit")]
public sealed class NetworkGuardHandlerTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task SendAsync_FakeHostAndFakeHandler_PassesTheRequestToTheFake()
    {
        // Arrange
        var fake = FakeHttpMessageHandler.Returning(HttpStatusCode.OK, "{}", SampleJson.Json);
        using var invoker = new HttpMessageInvoker(new NetworkGuardHandler(fake));
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(NetworkGuardHandler.BaseAddress, "ping"));

        // Act
        using var response = await invoker.SendAsync(request, Ct);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("https://apexpost.test/ping", Assert.Single(fake.Requests).Uri.AbsoluteUri);
    }

    [Theory]
    [InlineData("https://localhost:7310/api/v2/shipments")]
    [InlineData("https://api.apexpost.example/api/v2/shipments")]
    [InlineData("http://10.0.2.2:5310/api/v2/shipments")]
    public async Task SendAsync_OtherHost_FailsTheTestBeforeTheFakeIsCalled(string url)
    {
        // Arrange
        var fake = FakeHttpMessageHandler.Returning(HttpStatusCode.OK, "{}", SampleJson.Json);
        using var invoker = new HttpMessageInvoker(new NetworkGuardHandler(fake));
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(url));

        // Act
        var failure = await Assert.ThrowsAsync<FailException>(() => invoker.SendAsync(request, Ct));

        // Assert
        Assert.Contains("was blocked", failure.Message, StringComparison.Ordinal);
        Assert.Empty(fake.Requests);
    }

    [Fact]
    public async Task SendAsync_RealSocketsHandlerBelow_FailsTheTestWithoutSending()
    {
        // Arrange
        var real = new LoggingPassThrough { InnerHandler = new SocketsHttpHandler() };
        using var invoker = new HttpMessageInvoker(new NetworkGuardHandler(real));
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(NetworkGuardHandler.BaseAddress, "ping"));

        // Act
        var failure = await Assert.ThrowsAsync<FailException>(() => invoker.SendAsync(request, Ct));

        // Assert
        Assert.Contains("real network handler", failure.Message, StringComparison.Ordinal);
        Assert.Equal(0, real.Calls);
    }

    private sealed class LoggingPassThrough : DelegatingHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Calls++;
            return base.SendAsync(request, cancellationToken);
        }
    }
}
