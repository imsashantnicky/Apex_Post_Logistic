using ApexPost.Courier.Domain.Enums;

namespace ApexPost.Courier.Domain.Scans;

/// <summary>How many scans of one <see cref="Enums.ScanType"/> a courier has recorded.</summary>
/// <param name="ScanType">The scan type being counted.</param>
/// <param name="Count">The number of scans of that type.</param>
public sealed record ScanTypeCount(ScanType ScanType, int Count);
