using System.Collections.ObjectModel;
using System.Globalization;
using ApexPost.Courier.ApiClient.Auth;
using ApexPost.Courier.Domain.Ports;
using ApexPost.Courier.Offline.Outbox;
using ApexPost.Courier.Presentation.Mvvm;
using ApexPost.Courier.Presentation.Ports;

namespace ApexPost.Courier.Presentation.ViewModels;

/// <summary>
/// Sync Status page: when the last sync ran and what it did, the circuit breaker state, the last problems
/// (stop reasons and refused scans) and the scans the server refused, plus "Sync now" and "Sign out".
/// It is a singleton that follows every outbox run, also the ones started while the page is hidden.
/// </summary>
public sealed class SyncStatusViewModel : ObservableObject, IDisposable
{
    /// <summary>How many problems the page keeps (newest first).</summary>
    public const int MaximumProblems = 5;

    /// <summary>Shown when "Sync now" found nothing to upload or another run was busy.</summary>
    public const string NothingToUpload = "Nothing to upload (or a sync is already running).";

    private const string ButtonTrigger = "button";

    private readonly ISyncRunner _sync;
    private readonly IScanQueries _queries;
    private readonly IAccessTokenStore _tokens;
    private readonly IAppNavigator _navigator;
    private readonly IUiDispatcher _dispatcher;
    private readonly TimeProvider _time;
    private string _lastSync = "Not synced yet in this session.";
    private string? _lastResult;
    private bool _lastResultIsError;
    private string _courier = "Not signed in";

    /// <summary>Creates the view model and starts following outbox runs.</summary>
    /// <param name="sync">Runs the outbox.</param>
    /// <param name="queries">Finds the scans the server refused.</param>
    /// <param name="tokens">Secure token store (courier id, sign out).</param>
    /// <param name="navigator">Opens sign-in after sign out.</param>
    /// <param name="dispatcher">Moves updates to the UI thread.</param>
    /// <param name="time">Clock for the last sync time.</param>
    /// <param name="banner">Global offline banner, which also knows the circuit state.</param>
    public SyncStatusViewModel(
        ISyncRunner sync,
        IScanQueries queries,
        IAccessTokenStore tokens,
        IAppNavigator navigator,
        IUiDispatcher dispatcher,
        TimeProvider time,
        ConnectionBannerViewModel banner)
    {
        ArgumentNullException.ThrowIfNull(sync);
        ArgumentNullException.ThrowIfNull(queries);
        ArgumentNullException.ThrowIfNull(tokens);
        ArgumentNullException.ThrowIfNull(navigator);
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(banner);
        (_sync, _queries, _tokens, _navigator, _dispatcher, _time) =
            (sync, queries, tokens, navigator, dispatcher, time);
        Banner = banner;
        SyncNowCommand = new AsyncCommand(SyncNowAsync);
        RefreshCommand = new AsyncCommand(RefreshAsync);
        SignOutCommand = new AsyncCommand(SignOutAsync);
        _sync.SyncCompleted += OnSyncCompleted;
    }

    /// <summary>Global offline banner; <see cref="ConnectionBannerViewModel.CircuitStateText"/> is shown here.</summary>
    public ConnectionBannerViewModel Banner { get; }

    /// <summary>"Signed in as CR-101".</summary>
    public string Courier
    {
        get => _courier;
        private set => SetProperty(ref _courier, value);
    }

    /// <summary>Local time of the last finished run.</summary>
    public string LastSync
    {
        get => _lastSync;
        private set => SetProperty(ref _lastSync, value);
    }

    /// <summary>Counts of the last run, for example "2 synced, 0 failed, 1 waiting".</summary>
    public string? LastResult
    {
        get => _lastResult;
        private set => SetProperty(ref _lastResult, value);
    }

    /// <summary>Whether the last run left scans failed or waiting.</summary>
    public bool LastResultIsError
    {
        get => _lastResultIsError;
        private set => SetProperty(ref _lastResultIsError, value);
    }

    /// <summary>The last <see cref="MaximumProblems"/> problems, newest first.</summary>
    public ObservableCollection<string> RecentProblems { get; } = [];

    /// <summary>Scans the server refused (sync status Failed).</summary>
    public ObservableCollection<ScanRow> FailedScans { get; } = [];

    /// <summary>Uploads the pending scans now.</summary>
    public AsyncCommand SyncNowCommand { get; }

    /// <summary>Reloads the courier and the failed scans (when the page appears).</summary>
    public AsyncCommand RefreshCommand { get; }

    /// <summary>Removes the token from the secure store and opens sign-in. Local scans are kept.</summary>
    public AsyncCommand SignOutCommand { get; }

    /// <inheritdoc />
    public void Dispose() => _sync.SyncCompleted -= OnSyncCompleted;

    private async Task SyncNowAsync()
    {
        LastResult = "Syncing…";
        LastResultIsError = false;
        var result = await _sync.SyncNowAsync(ButtonTrigger, CancellationToken.None).ConfigureAwait(true);
        if (result == OutboxResult.Empty)
        {
            LastResult = NothingToUpload;
        }

        await RefreshAsync().ConfigureAwait(true);
    }

    private async Task RefreshAsync()
    {
        var courierId = AccessTokenClaims.TryGetCourierId(
            await _tokens.GetAccessTokenAsync(CancellationToken.None).ConfigureAwait(true));
        Courier = courierId is null ? "Not signed in" : $"Signed in as {courierId}";

        var failed = await _queries.GetFailedAsync(CancellationToken.None).ConfigureAwait(true);
        FailedScans.Clear();
        foreach (var scan in failed.OrderByDescending(scan => scan.ScannedAtUtc))
        {
            FailedScans.Add(ScanRow.From(scan));
        }
    }

    private async Task SignOutAsync()
    {
        _tokens.Clear();
        Courier = "Not signed in";
        await _navigator.ShowLoginAsync().ConfigureAwait(true);
    }

    // Raised by the outbox on a background thread.
    private void OnSyncCompleted(object? sender, OutboxResult result) => _dispatcher.Post(() => Show(result));

    private void Show(OutboxResult result)
    {
        var now = _time.GetLocalNow();
        LastSync = now.ToString("g", CultureInfo.CurrentCulture);
        LastResult = string.Create(
            CultureInfo.CurrentCulture,
            $"{result.Synced} synced, {result.Failed} failed, {result.Deferred} waiting.");
        LastResultIsError = result.Failed > 0 || result.Deferred > 0;

        var time = now.ToString("t", CultureInfo.CurrentCulture);
        if (result.Failed > 0)
        {
            AddProblem($"{time} – the server refused {result.Failed} scan(s).");
        }

        if (result.StopReason is not null)
        {
            AddProblem($"{time} – {result.StopReason}");
        }

        RefreshCommand.Execute(null);
    }

    private void AddProblem(string text)
    {
        RecentProblems.Insert(0, text);
        while (RecentProblems.Count > MaximumProblems)
        {
            RecentProblems.RemoveAt(RecentProblems.Count - 1);
        }
    }
}
