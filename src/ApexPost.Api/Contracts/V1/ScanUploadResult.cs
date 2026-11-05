namespace ApexPost.Api.Contracts.V1;

/// <summary>
/// The answer to a scan upload: <c>{ accepted, duplicatesSkipped, rejected: [{ scanId, reason }] }</c>. Accepted and
/// duplicate scans become Synced on the device, rejected ones Failed.
/// </summary>
/// <param name="Accepted">Scans stored by this upload.</param>
/// <param name="DuplicatesSkipped">Scans already stored or repeated in the batch.</param>
/// <param name="Rejected">Scans the server refused.</param>
public sealed record ScanUploadResult(
    int Accepted,
    int DuplicatesSkipped,
    IReadOnlyList<RejectedScanResponse> Rejected)
{
    /// <summary>How many scans this upload stored.</summary>
    /// <example>1</example>
    public int Accepted { get; init; } = Accepted;

    /// <summary>How many scans were already stored, or repeated in the batch, and were skipped.</summary>
    /// <example>0</example>
    public int DuplicatesSkipped { get; init; } = DuplicatesSkipped;

    /// <summary>The scans the server refused, each with a reason; empty when all were valid.</summary>
    /// <example>[]</example>
    public IReadOnlyList<RejectedScanResponse> Rejected { get; init; } = Rejected;
}
