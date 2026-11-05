namespace ApexPost.Application.Tracking;

/// <summary>
/// The answer to a scan upload. Accepted and duplicate scans are stored on the server, so the device marks both
/// Synced; rejected scans are marked Failed.
/// </summary>
public sealed record ScanUploadResult(int Accepted, int DuplicatesSkipped, IReadOnlyList<RejectedScan> Rejected);
