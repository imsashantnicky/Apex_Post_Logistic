namespace ApexPost.Courier.ApiClient.Auth;

/// <summary>
/// Keeps the courier's access token. The app stores it in MAUI <c>SecureStorage</c>; tests use an in-memory fake.
/// </summary>
public interface IAccessTokenStore
{
    /// <summary>Returns the stored token, or <see langword="null"/> when the courier is not signed in.</summary>
    /// <param name="ct">Cancels the read.</param>
    Task<string?> GetAccessTokenAsync(CancellationToken ct);

    /// <summary>Stores a new token, replacing the old one.</summary>
    /// <param name="token">The bearer token (JWT).</param>
    /// <param name="ct">Cancels the write.</param>
    Task SaveAccessTokenAsync(string token, CancellationToken ct);

    /// <summary>Removes the stored token (sign out).</summary>
    void Clear();
}
