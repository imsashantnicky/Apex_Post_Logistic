using ApexPost.Application.Exceptions;

namespace ApexPost.Application.Tracking;

/// <summary>The size limits of tracking lists and scan uploads.</summary>
public static class TrackingLimits
{
    public const int DefaultPageSize = 20;
    public const int MaximumPageSize = 50;
    public const int MaximumScansPerUpload = 100;

    /// <summary>Rejects a page number below 1 and a page size outside 1..<see cref="MaximumPageSize"/>.</summary>
    public static void CheckPage(int page, int pageSize)
    {
        if (page < 1)
        {
            throw new ValidationFailedException("page", "Page must be 1 or more.");
        }

        if (pageSize is < 1 or > MaximumPageSize)
        {
            throw new ValidationFailedException("pageSize", $"Page size must be between 1 and {MaximumPageSize}.");
        }
    }

    /// <summary>Rejects an empty upload and one with more than <see cref="MaximumScansPerUpload"/> scans.</summary>
    public static void CheckUpload(int scanCount)
    {
        if (scanCount is < 1 or > MaximumScansPerUpload)
        {
            throw new ValidationFailedException(
                "scans",
                $"An upload must contain between 1 and {MaximumScansPerUpload} scans.");
        }
    }
}
