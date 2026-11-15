using ApexPost.Courier.ApiClient.Auth;

namespace ApexPost.Courier.Security;

/// <summary>
/// Keeps the access token in MAUI <see cref="SecureStorage"/> (Android Keystore, Windows DPAPI), never in
/// preferences, files or logs.
/// </summary>
public sealed class SecureStorageTokenStore : IAccessTokenStore
{
    private const string TokenKey = "apexpost.access_token";

    /// <inheritdoc />
    public Task<string?> GetAccessTokenAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return SecureStorage.Default.GetAsync(TokenKey);
    }

    /// <inheritdoc />
    public Task SaveAccessTokenAsync(string token, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        ct.ThrowIfCancellationRequested();
        return SecureStorage.Default.SetAsync(TokenKey, token.Trim());
    }

    /// <inheritdoc />
    public void Clear() => SecureStorage.Default.Remove(TokenKey);
}
