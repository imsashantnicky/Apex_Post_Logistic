namespace ApexPost.Courier.ApiClient.Contracts;

/// <summary>A scan the API refused, with the reason.</summary>
/// <param name="ScanId">Id of the refused scan.</param>
/// <param name="Reason">Why it was refused.</param>
public sealed record RejectedScan(string ScanId, string Reason);
