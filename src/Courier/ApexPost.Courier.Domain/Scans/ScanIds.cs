namespace ApexPost.Courier.Domain.Scans;

/// <summary>Creates and checks scan ids of the form <c>scan::{Guid}</c>.</summary>
public static class ScanIds
{
    /// <summary>The prefix every scan id starts with.</summary>
    public const string Prefix = "scan::";

    private const string GuidFormat = "D";

    /// <summary>Returns a new unique scan id.</summary>
    public static string New() => Prefix + Guid.NewGuid().ToString(GuidFormat);

    /// <summary>True when <paramref name="id"/> is the prefix followed by a GUID in standard format.</summary>
    public static bool IsValid(string? id) =>
        id is not null
        && id.StartsWith(Prefix, StringComparison.Ordinal)
        && Guid.TryParseExact(id[Prefix.Length..], GuidFormat, out _);
}
