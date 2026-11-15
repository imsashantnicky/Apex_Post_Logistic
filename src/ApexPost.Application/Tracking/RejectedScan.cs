namespace ApexPost.Application.Tracking;

/// <summary>A scan the server refused, with the reason; the device marks it Failed.</summary>
public sealed record RejectedScan(string ScanId, string Reason);
