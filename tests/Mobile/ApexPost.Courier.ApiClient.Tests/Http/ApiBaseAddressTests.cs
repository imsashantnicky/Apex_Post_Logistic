using ApexPost.Courier.ApiClient.Http;

namespace ApexPost.Courier.ApiClient.Tests.Http;

public sealed class ApiBaseAddressTests
{
    private const string Android = "https://10.0.2.2:7310";
    private const string Windows = "https://localhost:7310/";

    [Fact]
    public void Android_uses_the_emulator_host_alias_with_a_trailing_slash()
    {
        var address = ApiBaseAddress.Choose(Android, Windows, isAndroid: true);

        Assert.Equal("https://10.0.2.2:7310/", address.AbsoluteUri);
    }

    [Fact]
    public void Windows_uses_localhost()
    {
        var address = ApiBaseAddress.Choose(Android, Windows, isAndroid: false);

        Assert.Equal("https://localhost:7310/", address.AbsoluteUri);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("localhost:7310")]
    [InlineData("/api")]
    [InlineData("http://localhost:5310")]
    public void A_missing_relative_or_plain_http_address_is_refused(string? value)
    {
        var error = Assert.Throws<InvalidOperationException>(() => ApiBaseAddress.Choose(Android, value, isAndroid: false));

        Assert.Contains(ApiBaseAddress.WindowsKey, error.Message, StringComparison.Ordinal);
    }
}
