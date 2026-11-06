namespace ApexPost.Api.Security;

/// <summary>
/// The claim names the API reads from a bearer token, as dotnet user-jwts and /dev/token write them. Inbound claim
/// mapping is off, so these short names are used as they are.
/// </summary>
public static class ApiClaims
{
    /// <summary>The user name (for a courier its courier id).</summary>
    public const string Subject = "sub";

    /// <summary>A role; a token may carry several.</summary>
    public const string Role = "role";

    /// <summary>The courier id (CR- followed by 3 digits) of a courier token.</summary>
    public const string CourierId = "courierId";
}
