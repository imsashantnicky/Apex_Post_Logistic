using System.Collections.ObjectModel;
using ApexPost.Courier.Domain.Ports;
using ApexPost.Courier.Presentation.Mvvm;
using ApexPost.Courier.Presentation.Ports;

namespace ApexPost.Courier.Presentation.ViewModels;

/// <summary>
/// Pending Scans page: the scans still waiting for upload, oldest first, and a live count. The count comes from
/// the Part A change listener (<see cref="IPendingCountSource"/>), so it moves while the courier records scans or
/// a sync runs, and the list reloads with it.
/// </summary>
public sealed class PendingScansViewModel : ObservableObject, IDisposable
{
    private readonly IScanQueries _queries;
    private readonly IPendingCountSource _countSource;
    private readonly IUiDispatcher _dispatcher;
    private int _pendingCount;

    /// <summary>Creates the view model and starts following the live count.</summary>
    /// <param name="queries">Local scan queries.</param>
    /// <param name="countSource">Live pending count.</param>
    /// <param name="dispatcher">Moves updates to the UI thread.</param>
    /// <param name="banner">Global offline banner.</param>
    public PendingScansViewModel(
        IScanQueries queries,
        IPendingCountSource countSource,
        IUiDispatcher dispatcher,
        ConnectionBannerViewModel banner)
    {
        ArgumentNullException.ThrowIfNull(queries);
        ArgumentNullException.ThrowIfNull(countSource);
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(banner);
        (_queries, _countSource, _dispatcher) = (queries, countSource, dispatcher);
        Banner = banner;
        RefreshCommand = new AsyncCommand(RefreshAsync);
        _countSource.PendingCountChanged += OnPendingCountChanged;
    }

    /// <summary>Global offline banner.</summary>
    public ConnectionBannerViewModel Banner { get; }

    /// <summary>The pending scans, oldest first.</summary>
    public ObservableCollection<ScanRow> Scans { get; } = [];

    /// <summary>Live number of pending scans.</summary>
    public int PendingCount
    {
        get => _pendingCount;
        private set
        {
            if (SetProperty(ref _pendingCount, value))
            {
                OnPropertyChanged(nameof(Title));
                OnPropertyChanged(nameof(IsEmpty));
            }
        }
    }

    /// <summary>Heading with the count, for example "Pending scans: 3".</summary>
    public string Title => $"Pending scans: {_pendingCount}";

    /// <summary>Whether nothing waits for upload (shows the empty-state text).</summary>
    public bool IsEmpty => _pendingCount == 0;

    /// <summary>Reloads the list and the count from the device.</summary>
    public AsyncCommand RefreshCommand { get; }

    /// <inheritdoc />
    public void Dispose() => _countSource.PendingCountChanged -= OnPendingCountChanged;

    private async Task RefreshAsync()
    {
        var pending = await _queries.GetPendingAsync(CancellationToken.None).ConfigureAwait(true);
        Scans.Clear();
        foreach (var scan in pending.OrderBy(scan => scan.ScannedAtUtc))
        {
            Scans.Add(ScanRow.From(scan));
        }

        PendingCount = pending.Count;
    }

    // Raised by the Couchbase listener on a background thread.
    private void OnPendingCountChanged(object? sender, int pending) =>
        _dispatcher.Post(() =>
        {
            PendingCount = pending;
            RefreshCommand.Execute(null);
        });
}
