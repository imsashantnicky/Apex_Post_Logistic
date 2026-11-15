using ApexPost.Client.Consumer;

namespace ApexPost.Api.Tests.Client;

/// <summary>
/// The consumer's base URL always ends with a slash, so the generated client's relative paths (api/v2/...) are
/// appended to it instead of replacing its last segment.
/// </summary>
public sealed class ApexPostApiOptionsTests
{
    [Theory]
    [InlineData("http://localhost:5310", "http://localhost:5310/")]
    [InlineData("http://localhost:5310/", "http://localhost:5310/")]
    [InlineData("https://api.apexpost.example/tracking", "https://api.apexpost.example/tracking/")]
    public void Base_address_ends_with_a_slash(string configured, string expected)
    {
        var options = new ApexPostApiOptions { BaseUrl = new Uri(configured) };

        Assert.Equal(expected, options.BaseAddress().AbsoluteUri);
    }

    [Fact]
    public void Generated_paths_are_appended_to_a_base_address_with_a_path()
    {
        var options = new ApexPostApiOptions { BaseUrl = new Uri("https://api.apexpost.example/tracking") };

        var url = new Uri(options.BaseAddress(), "api/v2/shipments");

        Assert.Equal("https://api.apexpost.example/tracking/api/v2/shipments", url.AbsoluteUri);
    }

    [Fact]
    public void Missing_base_url_is_reported_by_name()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => new ApexPostApiOptions().BaseAddress());

        Assert.Contains("ApexPostApi:BaseUrl", exception.Message, StringComparison.Ordinal);
    }
}
