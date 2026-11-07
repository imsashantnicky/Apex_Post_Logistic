namespace ApexPost.Courier.ApiClient.Contracts;

/// <summary>Body of <c>POST /dev/token</c> (Development only).</summary>
/// <param name="CourierId">The fictional courier to sign in.</param>
public sealed record DevTokenRequest(string CourierId);
