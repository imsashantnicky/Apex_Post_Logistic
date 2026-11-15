using System.Buffers.Text;
using System.Text;
using ApexPost.Courier.ApiClient;

namespace ApexPost.Courier.Presentation.Tests.Support;

public static class TestApi
{
    public const string Json = "application/json";
    public const string Problem = "application/problem+json";

    public static ApexApiClient Client(CannedHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://localhost:7310/") });

    /// <summary>A JWT shape with the given courierId claim; the app does not check the signature.</summary>
    public static string CourierToken(string courierId) =>
        "eyJhbGciOiJIUzI1NiJ9."
        + Base64Url.EncodeToString(Encoding.UTF8.GetBytes($$"""{"courierId":"{{courierId}}","role":"Courier"}"""))
        + ".signature";
}
