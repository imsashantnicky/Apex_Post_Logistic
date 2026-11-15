using System.Net.Http.Headers;

namespace ApexPost.Courier.ApiClient.Auth;

/// <summary>
/// Adds <c>Authorization: Bearer &lt;token&gt;</c> to every request of the typed client, reading the token from
/// <see cref="IAccessTokenStore"/> on each call so a new sign-in applies at once. The token is never logged.
/// </summary>
/// <param name="tokens">Where the token is kept (SecureStorage in the app).</param>
public sealed class AuthHeaderHandler(IAccessTokenStore tokens) : DelegatingHandler
{
    /// <summary>The authentication scheme the API expects.</summary>
    public const string Scheme = "Bearer";

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // A header set by the caller (for example a one-off token) wins over the stored token.
        if (request.Headers.Authorization is null)
        {
            var token = await tokens.GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(token))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue(Scheme, token.Trim());
            }
        }

        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
