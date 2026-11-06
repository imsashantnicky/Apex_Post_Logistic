namespace ApexPost.Courier.ApiClient.Http;

/// <summary>
/// The only hosts whose development certificate the Debug app may accept: <c>localhost</c> and the Android
/// emulator's alias for the host PC, <c>10.0.2.2</c>. The bypass itself lives in the app under <c>#if DEBUG</c>.
/// </summary>
public static class DevelopmentHosts
{
    private static readonly string[] Allowed = ["localhost", "10.0.2.2"];

    /// <summary>Whether <paramref name="address"/> points at a local development host.</summary>
    /// <param name="address">The request URI, or <see langword="null"/>.</param>
    public static bool IsDevelopmentHost(Uri? address) =>
        address is { IsAbsoluteUri: true }
        && Allowed.Contains(address.IdnHost, StringComparer.OrdinalIgnoreCase);
}
