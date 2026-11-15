using ApexPost.Courier.ApiClient;
using ApexPost.Courier.ApiClient.Auth;
using ApexPost.Courier.ApiClient.Results;
using ApexPost.Courier.Domain.Scans;
using ApexPost.Courier.Presentation.Errors;
using ApexPost.Courier.Presentation.Mvvm;
using ApexPost.Courier.Presentation.Ports;

namespace ApexPost.Courier.Presentation.ViewModels;

/// <summary>
/// Sign-in page: either asks the Development-only <c>POST /dev/token</c> for a courier token or takes a pasted
/// token. The token goes straight into the secure store (SecureStorage in the app), never into a property that
/// stays on screen; afterwards the main tabs open and the scans that waited on the device are uploaded.
/// </summary>
public sealed class LoginViewModel : ObservableObject
{
    /// <summary>Shown when a pasted token has no usable <c>courierId</c> claim.</summary>
    public const string NotACourierToken = "That is not a courier token. It needs a courierId claim like CR-101.";

    /// <summary>Default courier id of the demo sign-in (fictional).</summary>
    public const string DemoCourierId = "CR-101";

    private const string SignInTrigger = "sign-in";

    private readonly ApexApiClient _api;
    private readonly IAccessTokenStore _tokens;
    private readonly IAppNavigator _navigator;
    private readonly ISyncRunner _sync;
    private string _courierId = DemoCourierId;
    private string _token = string.Empty;
    private string? _message;
    private bool _messageIsError;

    /// <summary>Creates the view model.</summary>
    /// <param name="api">Typed API client.</param>
    /// <param name="tokens">Secure token store.</param>
    /// <param name="navigator">Opens the main tabs after sign-in.</param>
    /// <param name="sync">Uploads waiting scans after sign-in.</param>
    public LoginViewModel(ApexApiClient api, IAccessTokenStore tokens, IAppNavigator navigator, ISyncRunner sync)
    {
        ArgumentNullException.ThrowIfNull(api);
        ArgumentNullException.ThrowIfNull(tokens);
        ArgumentNullException.ThrowIfNull(navigator);
        ArgumentNullException.ThrowIfNull(sync);
        (_api, _tokens, _navigator, _sync) = (api, tokens, navigator, sync);
        SignInCommand = new AsyncCommand(SignInAsync);
        UseTokenCommand = new AsyncCommand(UseTokenAsync);
    }

    /// <summary>API root, shown small so the demo makes clear which server the app talks to.</summary>
    public string ApiAddress => $"API: {_api.BaseAddress}";

    /// <summary>Courier id for the Development sign-in.</summary>
    public string CourierId
    {
        get => _courierId;
        set => SetProperty(ref _courierId, value ?? string.Empty);
    }

    /// <summary>Pasted access token; cleared as soon as it is used.</summary>
    public string Token
    {
        get => _token;
        set => SetProperty(ref _token, value ?? string.Empty);
    }

    /// <summary>Status or error text above the sign-in cards.</summary>
    public string? Message
    {
        get => _message;
        private set
        {
            if (SetProperty(ref _message, value))
            {
                OnPropertyChanged(nameof(HasMessage));
            }
        }
    }

    /// <summary>Whether <see cref="Message"/> has text.</summary>
    public bool HasMessage => !string.IsNullOrEmpty(_message);

    /// <summary>Whether <see cref="Message"/> is an error (shown in red).</summary>
    public bool MessageIsError
    {
        get => _messageIsError;
        private set => SetProperty(ref _messageIsError, value);
    }

    /// <summary>Signs in through <c>POST /dev/token</c>.</summary>
    public AsyncCommand SignInCommand { get; }

    /// <summary>Signs in with the pasted token.</summary>
    public AsyncCommand UseTokenCommand { get; }

    /// <summary>Shows why the courier was sent here, for example an expired session.</summary>
    /// <param name="message">The text, shown as an error.</param>
    public void ShowError(string message)
    {
        Message = message;
        MessageIsError = true;
    }

    private async Task SignInAsync()
    {
        var courierId = CourierId.Trim().ToUpperInvariant();
        CourierId = courierId;
        if (!ScanRules.IsValidCourierId(courierId))
        {
            ShowError(FailureMessages.InvalidCourierId);
            return;
        }

        Message = "Signing in…";
        MessageIsError = false;
        var result = await _api.RequestDevelopmentTokenAsync(courierId, CancellationToken.None).ConfigureAwait(true);
        if (result.IsSuccess)
        {
            await CompleteSignInAsync(result.Value.AccessToken).ConfigureAwait(true);
        }
        else
        {
            ShowError(FriendlyError.Describe(result.Failure));
        }
    }

    private async Task UseTokenAsync()
    {
        var token = Token.Trim();
        Token = string.Empty;
        if (AccessTokenClaims.TryGetCourierId(token) is null)
        {
            ShowError(NotACourierToken);
            return;
        }

        await CompleteSignInAsync(token).ConfigureAwait(true);
    }

    private async Task CompleteSignInAsync(string token)
    {
        await _tokens.SaveAccessTokenAsync(token, CancellationToken.None).ConfigureAwait(true);
        Message = null;
        MessageIsError = false;
        await _navigator.ShowMainAsync().ConfigureAwait(true);
        _sync.SyncInBackground(SignInTrigger);
    }
}
