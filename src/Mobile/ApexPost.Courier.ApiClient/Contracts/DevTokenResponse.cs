namespace ApexPost.Courier.ApiClient.Contracts;

/// <summary>Answer of <c>POST /dev/token</c>.</summary>
/// <param name="AccessToken">The signed JWT. It is stored in SecureStorage only and never shown or logged.</param>
/// <param name="TokenType">Always <c>Bearer</c>.</param>
/// <param name="ExpiresAtUtc">When the token expires.</param>
/// <param name="CourierId">The courier the token belongs to.</param>
public sealed record DevTokenResponse(string AccessToken, string TokenType, DateTime ExpiresAtUtc, string CourierId);
