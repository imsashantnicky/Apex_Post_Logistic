using System.Net;
using System.Text.Json;
using ApexPost.Courier.ApiClient.Results;
using ApexPost.Courier.ApiClient.Tests.Support;

namespace ApexPost.Courier.ApiClient.Tests.Client;

public sealed class DevelopmentTokenTests
{
    private const string Answer = """
        { "accessToken": "header.payload.signature", "tokenType": "Bearer",
          "expiresAtUtc": "2026-11-07T10:00:00Z", "courierId": "CR-101" }
        """;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_courier_id_is_posted_to_dev_token_and_the_token_is_returned()
    {
        var handler = FakeHttpMessageHandler.Returning(HttpStatusCode.OK, Answer, SampleJson.Json);

        var result = await ClientFactory.Create(handler).RequestDevelopmentTokenAsync("CR-101", Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal("header.payload.signature", result.Value.AccessToken);
        var sent = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, sent.Method);
        Assert.Equal("/dev/token", sent.Uri.AbsolutePath);
        using var body = JsonDocument.Parse(sent.Body!);
        Assert.Equal("CR-101", body.RootElement.GetProperty("courierId").GetString());
    }

    [Fact]
    public async Task A_malformed_courier_id_never_leaves_the_device()
    {
        var handler = FakeHttpMessageHandler.Returning(HttpStatusCode.OK, Answer, SampleJson.Json);

        var result = await ClientFactory.Create(handler).RequestDevelopmentTokenAsync("courier1", Ct);

        Assert.False(result.IsSuccess);
        Assert.Equal(ApiFailureKind.InvalidInput, result.Failure.Kind);
        Assert.Equal(FailureMessages.InvalidCourierId, result.Failure.Message);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Outside_Development_the_404_is_a_typed_failure()
    {
        var handler = FakeHttpMessageHandler.Returning(HttpStatusCode.NotFound, null, SampleJson.Json);

        var result = await ClientFactory.Create(handler).RequestDevelopmentTokenAsync("CR-101", Ct);

        Assert.False(result.IsSuccess);
        Assert.Equal(ApiFailureKind.NotFound, result.Failure.Kind);
    }
}
