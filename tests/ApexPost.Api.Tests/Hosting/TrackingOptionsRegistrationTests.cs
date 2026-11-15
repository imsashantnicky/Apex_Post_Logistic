using ApexPost.Api.Composition;
using ApexPost.Application.Tracking;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ApexPost.Api.Tests.Hosting;

/// <summary>The API binds the "Tracking" section and refuses a scan age limit below one hour.</summary>
[Trait("Category", "Unit")]
public sealed class TrackingOptionsRegistrationTests
{
    private const string MaxScanAgeKey = "Tracking:MaxScanAgeHours";

    [Fact]
    public void AddApexApi_NoTrackingSection_UsesDefault72Hours()
    {
        // Arrange
        using var provider = ProviderWith(new Dictionary<string, string?>());

        // Act
        var options = provider.GetRequiredService<IOptions<TrackingOptions>>().Value;

        // Assert
        Assert.Equal(TrackingOptions.DefaultMaxScanAgeHours, options.MaxScanAgeHours);
        Assert.Equal(72, options.MaxScanAgeHours);
    }

    [Fact]
    public void AddApexApi_MaxScanAgeHoursConfigured_BindsValue()
    {
        // Arrange
        using var provider = ProviderWith(new Dictionary<string, string?> { [MaxScanAgeKey] = "48" });

        // Act
        var options = provider.GetRequiredService<IOptions<TrackingOptions>>().Value;

        // Assert
        Assert.Equal(48, options.MaxScanAgeHours);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    public void AddApexApi_MaxScanAgeHoursBelowOne_ThrowsOptionsValidationException(string value)
    {
        // Arrange
        using var provider = ProviderWith(new Dictionary<string, string?> { [MaxScanAgeKey] = value });

        // Act
        var exception = Assert.Throws<OptionsValidationException>(() =>
            provider.GetRequiredService<IOptions<TrackingOptions>>().Value);

        // Assert
        Assert.Equal("Tracking:MaxScanAgeHours must be 1 or more.", Assert.Single(exception.Failures));
    }

    private static ServiceProvider ProviderWith(Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        return new ServiceCollection().AddApexApi(configuration).BuildServiceProvider();
    }
}
