namespace ApexPost.Courier.ApiClient.Contracts;

/// <summary>Answer to a scan upload. Accepted and duplicate scans become Synced; rejected scans become Failed.</summary>
/// <param name="Accepted">Scans stored by this upload.</param>
/// <param name="DuplicatesSkipped">Scans the server already had.</param>
/// <param name="Rejected">Scans refused, with reasons.</param>
public sealed record ScanUploadResult(int Accepted, int DuplicatesSkipped, IReadOnlyList<RejectedScan> Rejected);
