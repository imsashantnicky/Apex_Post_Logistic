namespace ApexPost.Api.Contracts.V1;

/// <summary>A scan the server refused; the device marks it Failed.</summary>
public sealed record RejectedScanResponse(string ScanId, string Reason);
