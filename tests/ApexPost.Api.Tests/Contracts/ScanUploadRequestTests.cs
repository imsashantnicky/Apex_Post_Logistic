using ApexPost.Api.Contracts.V1;
using ApexPost.Api.Tests.Support;
using ApexPost.Application.Tracking;

namespace ApexPost.Api.Tests.Contracts;

/// <summary>An upload holds 1 to 100 scans: 0, 1, 100 and 101 at the contract.</summary>
[Trait("Category", "Unit")]
public sealed class ScanUploadRequestTests
{
    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(TrackingLimits.MaximumScansPerUpload, true)]
    [InlineData(TrackingLimits.MaximumScansPerUpload + 1, false)]
    public void Validate_ScanCount_PassesOnlyForOneToOneHundredScans(int count, bool valid)
    {
        // Arrange
        var request = new ScanUploadRequest { Scans = [.. Enumerable.Range(0, count).Select(_ => Requests.Scan())] };

        // Act
        var failed = ContractValidator.FailedMembers(request);

        // Assert
        Assert.Equal(valid, !failed.Contains(nameof(request.Scans)));
    }
}
