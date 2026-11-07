namespace ApexPost.Courier.ApiClient.Tests.Support;

public static class ClientFactory
{
    public static readonly Uri BaseAddress = new("https://localhost:7310/");

    public static ApexApiClient Create(HttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = BaseAddress });
}
