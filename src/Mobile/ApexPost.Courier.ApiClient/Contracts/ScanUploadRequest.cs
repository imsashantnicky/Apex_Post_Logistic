namespace ApexPost.Courier.ApiClient.Contracts;

/// <summary>Body of <c>POST /api/v2/shipments/{trackingNumber}/scans</c>.</summary>
/// <param name="Scans">Scans to upload (at least one).</param>
public sealed record ScanUploadRequest(IReadOnlyList<ScanItem> Scans);
