using ApexPost.Courier.ApiClient.Http;

namespace ApexPost.Courier.ApiClient.Tests.Http;

public sealed class DevelopmentHostsTests
{
    [Theory]
    [InlineData("https://localhost:7310/api/v2/shipments")]
    [InlineData("https://LOCALHOST:7310/")]
    [InlineData("https://10.0.2.2:7310/")]
    public void Localhost_and_the_android_emulator_alias_are_development_hosts(string address)
    {
        Assert.True(DevelopmentHosts.IsDevelopmentHost(new Uri(address)));
    }

    [Theory]
    [InlineData("https://api.apexpost.example/")]
    [InlineData("https://localhost.apexpost.example/")]
    [InlineData("https://10.0.2.3:7310/")]
    [InlineData("https://127.0.0.1:7310/")]
    [InlineData("https://192.168.1.10:7310/")]
    public void Every_other_host_is_not(string address)
    {
        Assert.False(DevelopmentHosts.IsDevelopmentHost(new Uri(address)));
    }

    [Fact]
    public void A_missing_or_relative_address_is_not()
    {
        Assert.False(DevelopmentHosts.IsDevelopmentHost(null));
        Assert.False(DevelopmentHosts.IsDevelopmentHost(new Uri("/api", UriKind.Relative)));
    }
}
