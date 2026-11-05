namespace ApexPost.Api.Contracts.V1;

/// <summary>
/// The answer to a scan upload: <c>{ accepted, duplicatesSkipped, rejected: [{ scanId, reason }] }</c>. Accepted and
/// duplicate scans become Synced on the device, rejected ones Failed.
/// </summary>
public sealed record ScanUploadResult(
    int Accepted,
    int DuplicatesSkipped,
    IReadOnlyList<RejectedScanResponse> Rejected);
