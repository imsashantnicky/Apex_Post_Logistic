using ApexPost.Api.Contracts.V1;
using ApexPost.Api.Tests.Support;
using ApexPost.Application.Tracking;

namespace ApexPost.Api.Tests.Contracts;

/// <summary>An upload holds 1 to 100 scans.</summary>
public sealed class ScanUploadRequestTests
{
    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(TrackingLimits.MaximumScansPerUpload, true)]
    [InlineData(TrackingLimits.MaximumScansPerUpload + 1, false)]
    public void An_upload_holds_1_to_100_scans(int count, bool valid)
    {
        var request = new ScanUploadRequest { Scans = [.. Enumerable.Range(0, count).Select(_ => Requests.Scan())] };

        Assert.Equal(valid, !ContractValidator.FailedMembers(request).Contains(nameof(request.Scans)));
    }
}
