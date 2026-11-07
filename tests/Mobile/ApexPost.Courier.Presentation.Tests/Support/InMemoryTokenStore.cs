using ApexPost.Courier.ApiClient.Auth;

namespace ApexPost.Courier.Presentation.Tests.Support;

public sealed class InMemoryTokenStore(string? token = null) : IAccessTokenStore
{
    public string? Token { get; private set; } = token;

    public Task<string?> GetAccessTokenAsync(CancellationToken ct) => Task.FromResult(Token);

    public Task SaveAccessTokenAsync(string token, CancellationToken ct)
    {
        Token = token;
        return Task.CompletedTask;
    }

    public void Clear() => Token = null;
}
