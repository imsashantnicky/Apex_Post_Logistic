using System.ComponentModel.DataAnnotations;
using ApexPost.Application.Tracking;

namespace ApexPost.Api.Contracts.V1;

/// <summary>The body of POST /api/v1/scans: a batch of device scans, which may safely be sent again.</summary>
public sealed class ScanUploadRequest
{
    /// <summary>1 to 100 scans.</summary>
    [Required]
    [Length(1, TrackingLimits.MaximumScansPerUpload)]
    public IReadOnlyList<ScanItem> Scans { get; init; } = [];
}
