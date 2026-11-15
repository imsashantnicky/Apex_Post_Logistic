namespace ApexPost.Api.Security;

/// <summary>A courier token from POST /dev/token.</summary>
/// <param name="AccessToken">The signed JWT to send as Authorization: Bearer.</param>
/// <param name="TokenType">Always Bearer.</param>
/// <param name="ExpiresAtUtc">When the token stops being accepted.</param>
/// <param name="CourierId">The courier the token belongs to (its courierId claim).</param>
public sealed record DevTokenResponse(string AccessToken, string TokenType, DateTime ExpiresAtUtc, string CourierId);
