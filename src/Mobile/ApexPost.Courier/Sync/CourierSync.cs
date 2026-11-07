using ApexPost.Courier.ApiClient.Auth;
using ApexPost.Courier.Offline.Outbox;
using Microsoft.Extensions.Logging;

namespace ApexPost.Courier.Sync;

/// <summary>
/// Starts outbox runs: at start-up, after sign-in, when the device gets Internet access again, and on request.
/// The outbox is single-flight, so a trigger that arrives during a run returns at once. A 401 clears the token
/// from SecureStorage and raises <see cref="SignInRequired"/>; network problems leave the scans Pending.
/// </summary>
public sealed partial class CourierSync : IDisposable
{
    private readonly OutboxService _outbox;
    private readonly IAccessTokenStore _tokens;
    private readonly IConnectivity _connectivity;
    private readonly ILogger<CourierSync> _logger;
    private bool _listening;

    /// <summary>Creates the sync service.</summary>
    /// <param name="outbox">The outbox with the HTTP uploader.</param>
    /// <param name="tokens">SecureStorage token store.</param>
    /// <param name="connectivity">MAUI connectivity, for the reconnect trigger.</param>
    /// <param name="logger">Logs triggers and results (never the token).</param>
    public CourierSync(
        OutboxService outbox,
        IAccessTokenStore tokens,
        IConnectivity connectivity,
        ILogger<CourierSync> logger)
    {
        _outbox = outbox;
        _tokens = tokens;
        _connectivity = connectivity;
        _logger = logger;
    }

    /// <summary>Raised after every run that uploaded or deferred something, on a background thread.</summary>
    public event EventHandler<OutboxResult>? SyncCompleted;

    /// <summary>Raised when the server answered 401; the token has already been removed.</summary>
    public event EventHandler? SignInRequired;

    /// <summary>Starts listening for connectivity changes. Calling it again does nothing.</summary>
    public void StartListening()
    {
        if (_listening)
        {
            return;
        }

        _listening = true;
        _connectivity.ConnectivityChanged += OnConnectivityChanged;
    }

    /// <summary>Runs the outbox once in the background; never throws.</summary>
    /// <param name="trigger">Why the run starts, for the log.</param>
    public void SyncInBackground(string trigger) => _ = SyncNowAsync(trigger, CancellationToken.None);

    /// <summary>Runs the outbox once. Without a stored token nothing is sent.</summary>
    /// <param name="trigger">Why the run starts, for the log.</param>
    /// <param name="ct">Stops the run between batches.</param>
    /// <returns>The run's counts, or <see cref="OutboxResult.Empty"/> when nothing ran.</returns>
    public async Task<OutboxResult> SyncNowAsync(string trigger, CancellationToken ct)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(await _tokens.GetAccessTokenAsync(ct).ConfigureAwait(false)))
            {
                LogNotSignedIn(_logger, trigger);
                return OutboxResult.Empty;
            }

            LogSyncStarted(_logger, trigger);
            var result = await _outbox.ProcessPendingAsync(includeFailed: false, ct).ConfigureAwait(false);
            if (result.SignInRequired)
            {
                _tokens.Clear();
                SignInRequired?.Invoke(this, EventArgs.Empty);
            }

            if (result != OutboxResult.Empty)
            {
                SyncCompleted?.Invoke(this, result);
            }

            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            // A trigger is fire-and-forget: a local database error is logged and the next trigger tries again.
            LogSyncFailed(_logger, trigger, ex);
            return OutboxResult.Empty;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_listening)
        {
            _connectivity.ConnectivityChanged -= OnConnectivityChanged;
            _listening = false;
        }
    }

    private void OnConnectivityChanged(object? sender, ConnectivityChangedEventArgs e)
    {
        if (e.NetworkAccess == NetworkAccess.Internet)
        {
            SyncInBackground("connectivity");
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Sync ({Trigger}) skipped: not signed in")]
    private static partial void LogNotSignedIn(ILogger logger, string trigger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Sync started ({Trigger})")]
    private static partial void LogSyncStarted(ILogger logger, string trigger);

    [LoggerMessage(Level = LogLevel.Error, Message = "Sync ({Trigger}) failed")]
    private static partial void LogSyncFailed(ILogger logger, string trigger, Exception exception);
}
