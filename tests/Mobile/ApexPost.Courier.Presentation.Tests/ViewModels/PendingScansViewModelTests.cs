using ApexPost.Courier.ApiClient.Resilience;
using ApexPost.Courier.Domain.Enums;
using ApexPost.Courier.Presentation.Tests.Support;
using ApexPost.Courier.Presentation.ViewModels;

namespace ApexPost.Courier.Presentation.Tests.ViewModels;

public sealed class PendingScansViewModelTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 7, 8, 0, 0, TimeSpan.Zero);

    private readonly InMemoryScanStore _store = new();
    private readonly FakePendingCountSource _source = new();
    private readonly InlineDispatcher _dispatcher = new();

    [Fact]
    public async Task Refresh_lists_only_pending_scans_oldest_first()
    {
        _store.Add("APX-20000002", SyncStatus.Pending, Start.AddMinutes(10));
        _store.Add("APX-20000001", SyncStatus.Pending, Start);
        _store.Add("APX-20000003", SyncStatus.Synced, Start.AddMinutes(5));
        _store.Add("APX-20000004", SyncStatus.Failed, Start.AddMinutes(6));
        using var viewModel = Create();

        await viewModel.RefreshCommand.ExecuteAsync();

        Assert.Equal(["APX-20000001", "APX-20000002"], viewModel.Scans.Select(row => row.TrackingNumber));
        Assert.Equal(2, viewModel.PendingCount);
        Assert.Equal("Pending scans: 2", viewModel.Title);
        Assert.False(viewModel.IsEmpty);
    }

    [Fact]
    public async Task The_live_count_from_the_listener_updates_the_count_and_reloads_the_list()
    {
        using var viewModel = Create();
        await viewModel.RefreshCommand.ExecuteAsync();
        Assert.True(viewModel.IsEmpty);

        _store.Add("APX-20000001", SyncStatus.Pending, Start);
        _source.Raise(1);

        Assert.Equal(1, _dispatcher.Posted);
        Assert.Equal(1, viewModel.PendingCount);
        Assert.Equal("APX-20000001", Assert.Single(viewModel.Scans).TrackingNumber);
    }

    [Fact]
    public void Count_changes_raise_title_and_empty_state_changes()
    {
        using var viewModel = Create();
        var changed = new List<string?>();
        viewModel.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        _source.Raise(3);

        Assert.Contains(nameof(PendingScansViewModel.PendingCount), changed);
        Assert.Contains(nameof(PendingScansViewModel.Title), changed);
        Assert.Contains(nameof(PendingScansViewModel.IsEmpty), changed);
    }

    [Fact]
    public void Dispose_stops_listening()
    {
        var viewModel = Create();
        Assert.True(_source.HasSubscribers);

        viewModel.Dispose();

        Assert.False(_source.HasSubscribers);
    }

    [Fact]
    public void A_row_shows_type_location_and_time()
    {
        var scan = _store.Add("APX-20000001", SyncStatus.Pending, Start);

        var row = ScanRow.From(scan);

        Assert.Equal(scan.Id, row.Id);
        Assert.StartsWith("Pickup · Central Hub · ", row.Details, StringComparison.Ordinal);
    }

    private PendingScansViewModel Create() =>
        new(_store, _source, _dispatcher, new ConnectionBannerViewModel(new CircuitStateTracker(), _dispatcher));
}
