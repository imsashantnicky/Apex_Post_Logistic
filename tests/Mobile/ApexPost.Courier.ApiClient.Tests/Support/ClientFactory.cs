namespace ApexPost.Courier.ApiClient.Tests.Support;

/// <summary>Builds a typed client on a fake handler behind the <see cref="NetworkGuardHandler"/>.</summary>
public static class ClientFactory
{
    public static readonly Uri BaseAddress = NetworkGuardHandler.BaseAddress;

    public static ApexApiClient Create(HttpMessageHandler handler) =>
        new(new HttpClient(new NetworkGuardHandler(handler)) { BaseAddress = BaseAddress });
}
