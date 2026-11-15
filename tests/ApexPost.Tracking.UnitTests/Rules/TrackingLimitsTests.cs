using ApexPost.Application.Exceptions;
using ApexPost.Application.Tracking;

namespace ApexPost.Tracking.UnitTests.Rules;

/// <summary>
/// The page, page-size and upload-size limits at, just below and just above each boundary, with the exact field
/// name and message of the <see cref="ValidationFailedException"/>. The service tests only check that the service
/// calls these checks; the boundaries are proved here, once.
/// </summary>
[Trait("Category", "Unit")]
public sealed class TrackingLimitsTests
{
    private const string PageMessage = "Page must be 1 or more.";
    private const string PageSizeMessage = "Page size must be between 1 and 50.";
    private const string UploadMessage = "An upload must contain between 1 and 100 scans.";
    private const string SummaryMessage = "One or more fields are not valid.";

    [Theory]
    [InlineData(1, 1)]
    [InlineData(1, TrackingLimits.DefaultPageSize)]
    [InlineData(1, TrackingLimits.MaximumPageSize)]
    [InlineData(2, TrackingLimits.MaximumPageSize)]
    [InlineData(int.MaxValue, 1)]
    public void CheckPage_PageAndPageSizeInsideTheLimits_DoesNotThrow(int page, int pageSize)
    {
        // Arrange
        var check = () => TrackingLimits.CheckPage(page, pageSize);

        // Act
        var error = Record.Exception(check);

        // Assert
        Assert.Null(error);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void CheckPage_PageBelowOne_ThrowsValidationFailedExceptionForPage(int page)
    {
        // Arrange
        var check = () => TrackingLimits.CheckPage(page, TrackingLimits.DefaultPageSize);

        // Act
        var error = Assert.Throws<ValidationFailedException>(check);

        // Assert
        Assert.Equal(SummaryMessage, error.Message);
        var field = Assert.Single(error.Errors);
        Assert.Equal("page", field.Key);
        Assert.Equal([PageMessage], field.Value);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(TrackingLimits.MaximumPageSize + 1)]
    [InlineData(int.MaxValue)]
    public void CheckPage_PageSizeOutsideOneToFifty_ThrowsValidationFailedExceptionForPageSize(int pageSize)
    {
        // Arrange
        var check = () => TrackingLimits.CheckPage(1, pageSize);

        // Act
        var error = Assert.Throws<ValidationFailedException>(check);

        // Assert
        var field = Assert.Single(error.Errors);
        Assert.Equal("pageSize", field.Key);
        Assert.Equal([PageSizeMessage], field.Value);
    }

    [Fact]
    public void CheckPage_PageAndPageSizeBothWrong_ReportsThePageFirst()
    {
        // Arrange
        var check = () => TrackingLimits.CheckPage(0, 0);

        // Act
        var error = Assert.Throws<ValidationFailedException>(check);

        // Assert
        Assert.Equal("page", Assert.Single(error.Errors).Key);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(TrackingLimits.MaximumScansPerUpload - 1)]
    [InlineData(TrackingLimits.MaximumScansPerUpload)]
    public void CheckUpload_OneToOneHundredScans_DoesNotThrow(int scanCount)
    {
        // Arrange
        var check = () => TrackingLimits.CheckUpload(scanCount);

        // Act
        var error = Record.Exception(check);

        // Assert
        Assert.Null(error);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(TrackingLimits.MaximumScansPerUpload + 1)]
    public void CheckUpload_EmptyOrMoreThanOneHundredScans_ThrowsValidationFailedExceptionForScans(int scanCount)
    {
        // Arrange
        var check = () => TrackingLimits.CheckUpload(scanCount);

        // Act
        var error = Assert.Throws<ValidationFailedException>(check);

        // Assert
        Assert.Equal(SummaryMessage, error.Message);
        var field = Assert.Single(error.Errors);
        Assert.Equal("scans", field.Key);
        Assert.Equal([UploadMessage], field.Value);
    }

    [Fact]
    public void Limits_PublishedValues_AreTwentyFiftyAndOneHundred()
    {
        // Arrange
        int[] expected = [20, 50, 100];

        // Act
        int[] actual =
            [TrackingLimits.DefaultPageSize, TrackingLimits.MaximumPageSize, TrackingLimits.MaximumScansPerUpload];

        // Assert
        Assert.Equal(expected, actual);
    }
}
