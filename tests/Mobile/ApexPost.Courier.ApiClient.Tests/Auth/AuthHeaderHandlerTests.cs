using System.Net;
using System.Net.Http.Headers;
using ApexPost.Courier.ApiClient.Auth;
using ApexPost.Courier.ApiClient.Tests.Support;

namespace ApexPost.Courier.ApiClient.Tests.Auth;

public sealed class AuthHeaderHandlerTests
{
    private const string Token = "eyJhbGciOiJIUzI1NiJ9.fake.signature";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_stored_token_is_sent_as_a_bearer_header()
    {
        var (client, inner) = CreateClient(new InMemoryTokenStore(Token));

        await client.GetShipmentAsync("APX-20000005", Ct);

        Assert.Equal($"Bearer {Token}", Assert.Single(inner.Requests).Authorization);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task No_header_is_sent_when_there_is_no_token(string? token)
    {
        var (client, inner) = CreateClient(new InMemoryTokenStore(token));

        await client.GetShipmentAsync("APX-20000005", Ct);

        Assert.Null(Assert.Single(inner.Requests).Authorization);
    }

    [Fact]
    public async Task The_token_is_read_on_every_request_so_a_new_sign_in_applies_at_once()
    {
        var store = new InMemoryTokenStore("first");
        var (client, inner) = CreateClient(store);

        await client.GetShipmentAsync("APX-20000005", Ct);
        await store.SaveAccessTokenAsync("second", Ct);
        await client.GetShipmentAsync("APX-20000005", Ct);
        store.Clear();
        await client.GetShipmentAsync("APX-20000005", Ct);

        Assert.Equal(["Bearer first", "Bearer second", null], inner.Requests.Select(request => request.Authorization));
    }

    [Fact]
    public async Task A_header_set_by_the_caller_is_not_overwritten()
    {
        var inner = FakeHttpMessageHandler.Returning(HttpStatusCode.NoContent, null, SampleJson.Json);
        using var handler = new AuthHeaderHandler(new InMemoryTokenStore(Token)) { InnerHandler = inner };
        using var invoker = new HttpMessageInvoker(handler);
        using var request = new HttpRequestMessage(HttpMethod.Get, ClientFactory.BaseAddress);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "caller-token");

        using var response = await invoker.SendAsync(request, Ct);

        Assert.Equal("Bearer caller-token", Assert.Single(inner.Requests).Authorization);
    }

    private static (ApexApiClient Client, FakeHttpMessageHandler Inner) CreateClient(IAccessTokenStore store)
    {
        var inner = FakeHttpMessageHandler.Returning(HttpStatusCode.OK, SampleJson.Shipment, SampleJson.Json);
        var handler = new AuthHeaderHandler(store) { InnerHandler = inner };
        return (ClientFactory.Create(handler), inner);
    }
}
