using ApexPost.Courier.ApiClient;
using ApexPost.Courier.ApiClient.Auth;
using ApexPost.Courier.Sync;

namespace ApexPost.Courier;

/// <summary>
/// Sign-in page. The token goes straight into SecureStorage (never Preferences, files or logs); after a sign-in
/// the app opens the main page and uploads the scans that waited on the device.
/// </summary>
public partial class LoginPage : ContentPage
{
    private static readonly Color ErrorColor = Color.FromArgb("#B3261E");

    private readonly ApexApiClient _api;
    private readonly IAccessTokenStore _tokens;
    private readonly AppNavigator _navigator;
    private readonly CourierSync _sync;

    /// <summary>Creates the page with its services.</summary>
    /// <param name="api">Typed API client, for the Development sign-in.</param>
    /// <param name="tokens">Secure token store.</param>
    /// <param name="navigator">Opens the main page after sign-in.</param>
    /// <param name="sync">Starts the upload after sign-in.</param>
    public LoginPage(ApexApiClient api, IAccessTokenStore tokens, AppNavigator navigator, CourierSync sync)
    {
        InitializeComponent();
        _api = api;
        _tokens = tokens;
        _navigator = navigator;
        _sync = sync;
        ApiAddressLabel.Text = $"API: {api.BaseAddress}";
    }

    /// <summary>Shows why the courier was sent here, for example an expired session.</summary>
    /// <param name="message">Text to show above the sign-in cards.</param>
    public void ShowMessage(string message)
    {
        StatusLabel.Text = message;
        StatusLabel.TextColor = ErrorColor;
    }

    private async void OnSignInClicked(object? sender, EventArgs e)
    {
        SignInButton.IsEnabled = false;
        var courierId = (CourierEntry.Text ?? string.Empty).Trim().ToUpperInvariant();
        var result = await _api.RequestDevelopmentTokenAsync(courierId, CancellationToken.None);
        SignInButton.IsEnabled = true;

        if (result.IsSuccess)
        {
            await CompleteSignInAsync(result.Value.AccessToken);
        }
        else
        {
            ShowMessage(result.Failure.Message);
        }
    }

    private async void OnUseTokenClicked(object? sender, EventArgs e)
    {
        var token = (TokenEntry.Text ?? string.Empty).Trim();
        TokenEntry.Text = string.Empty;
        if (AccessTokenClaims.TryGetCourierId(token) is null)
        {
            ShowMessage("That is not a courier token. It needs a courierId claim like CR-101.");
            return;
        }

        await CompleteSignInAsync(token);
    }

    private async Task CompleteSignInAsync(string token)
    {
        await _tokens.SaveAccessTokenAsync(token, CancellationToken.None);
        await _navigator.ShowMainAsync();
        _sync.SyncInBackground("sign-in");
    }
}
