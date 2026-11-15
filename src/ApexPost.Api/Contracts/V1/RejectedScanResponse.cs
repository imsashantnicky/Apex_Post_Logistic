namespace ApexPost.Api.Contracts.V1;

/// <summary>A scan the server refused; the device marks it Failed.</summary>
/// <param name="ScanId">The refused scan.</param>
/// <param name="Reason">Why it was refused.</param>
public sealed record RejectedScanResponse(string ScanId, string Reason)
{
    /// <summary>The device document id of the refused scan.</summary>
    /// <example>scan::7c9e6679-7425-40de-944b-e07fc1f90ae7</example>
    public string ScanId { get; init; } = ScanId;

    /// <summary>Why the scan was refused, in plain words.</summary>
    /// <example>The scan would move the status from Delivered back to InTransit.</example>
    public string Reason { get; init; } = Reason;
}
