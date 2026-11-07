using System.Buffers.Text;
using System.Text.Json;
using ApexPost.Courier.Domain.Scans;

namespace ApexPost.Courier.ApiClient.Auth;

/// <summary>
/// Reads the <c>courierId</c> claim from the payload of a JWT so the app can stamp new scans with it. The app does
/// not validate the token (the API does on every call); a token it cannot read simply gives no courier id.
/// </summary>
public static class AccessTokenClaims
{
    /// <summary>Name of the courier id claim the API issues.</summary>
    public const string CourierIdClaim = "courierId";

    /// <summary>Returns the well-formed courier id of <paramref name="token"/>, or <see langword="null"/>.</summary>
    /// <param name="token">A JWT (<c>header.payload.signature</c>).</param>
    public static string? TryGetCourierId(string? token)
    {
        var parts = token?.Trim().Split('.');
        if (parts is not { Length: 3 } || string.IsNullOrEmpty(parts[1]))
        {
            return null;
        }

        try
        {
            using var payload = JsonDocument.Parse(Base64Url.DecodeFromChars(parts[1]));
            return payload.RootElement.ValueKind == JsonValueKind.Object
                && payload.RootElement.TryGetProperty(CourierIdClaim, out var claim)
                && claim.ValueKind == JsonValueKind.String
                && ScanRules.IsValidCourierId(claim.GetString() ?? string.Empty)
                ? claim.GetString()
                : null;
        }
        catch (Exception ex) when (ex is FormatException or JsonException)
        {
            // Not base64url or not JSON: treat it as a token without a courier id.
            return null;
        }
    }
}
