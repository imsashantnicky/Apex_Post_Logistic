using ApexPost.Courier.ApiClient.Resilience;
using ApexPost.Courier.Domain.Enums;
using ApexPost.Courier.Offline.Outbox;
using ApexPost.Courier.Presentation.Tests.Support;
using ApexPost.Courier.Presentation.ViewModels;
using Polly.CircuitBreaker;

namespace ApexPost.Courier.Presentation.Tests.ViewModels;

public sealed class SyncStatusViewModelTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 7, 10, 15, 0, TimeSpan.Zero);

    private readonly FakeSyncRunner _sync = new();
    private readonly InMemoryScanStore _store = new();
    private readonly InMemoryTokenStore _tokens = new(TestApi.CourierToken("CR-101"));
    private readonly FakeNavigator _navigator = new();
    private readonly InlineDispatcher _dispatcher = new();
    private readonly CircuitStateTracker _circuit = new();

    [Fact]
    public void A_finished_run_sets_the_last_sync_time_and_counts()
    {
        using var viewModel = Create();

        _sync.Raise(new OutboxResult(3, 3, 0));

        Assert.Equal(Now.ToString("g", System.Globalization.CultureInfo.CurrentCulture), viewModel.LastSync);
        Assert.Equal("3 synced, 0 failed, 0 waiting.", viewModel.LastResult);
        Assert.False(viewModel.LastResultIsError);
        Assert.Empty(viewModel.RecentProblems);
    }

    [Fact]
    public void A_stopped_run_records_the_reason_as_a_problem()
    {
        using var viewModel = Create();

        _sync.Raise(new OutboxResult(4, 1, 0) { Deferred = 3, StopReason = "Cannot reach the ApexPost server." });

        Assert.True(viewModel.LastResultIsError);
        Assert.Equal("1 synced, 0 failed, 3 waiting.", viewModel.LastResult);
        Assert.EndsWith("Cannot reach the ApexPost server.", Assert.Single(viewModel.RecentProblems), StringComparison.Ordinal);
    }

    [Fact]
    public void Refused_scans_are_listed_and_counted_as_a_problem()
    {
        _store.Add("APX-20000001", SyncStatus.Failed, Now.AddMinutes(-5));
        _store.Add("APX-20000002", SyncStatus.Pending, Now.AddMinutes(-4));
        using var viewModel = Create();

        _sync.Raise(new OutboxResult(2, 1, 1));

        Assert.Contains("refused 1 scan(s)", Assert.Single(viewModel.RecentProblems), StringComparison.Ordinal);
        Assert.Equal("APX-20000001", Assert.Single(viewModel.FailedScans).TrackingNumber);
    }

    [Fact]
    public void Only_the_last_five_problems_are_kept_newest_first()
    {
        using var viewModel = Create();

        for (var run = 1; run <= 7; run++)
        {
            _sync.Raise(new OutboxResult(1, 0, 0) { Deferred = 1, StopReason = $"reason {run}" });
        }

        Assert.Equal(SyncStatusViewModel.MaximumProblems, viewModel.RecentProblems.Count);
        Assert.EndsWith("reason 7", viewModel.RecentProblems[0], StringComparison.Ordinal);
        Assert.EndsWith("reason 3", viewModel.RecentProblems[^1], StringComparison.Ordinal);
    }

    [Fact]
    public async Task Sync_now_runs_the_outbox_with_the_button_trigger()
    {
        using var viewModel = Create();
        _sync.NextResult = new OutboxResult(2, 2, 0);

        await viewModel.SyncNowCommand.ExecuteAsync();

        Assert.Equal(["button"], _sync.Triggers);
        Assert.Equal("2 synced, 0 failed, 0 waiting.", viewModel.LastResult);
    }

    [Fact]
    public async Task Sync_now_with_nothing_to_upload_says_so()
    {
        using var viewModel = Create();

        await viewModel.SyncNowCommand.ExecuteAsync();

        Assert.Equal(SyncStatusViewModel.NothingToUpload, viewModel.LastResult);
    }

    [Fact]
    public async Task Refresh_shows_the_signed_in_courier()
    {
        using var viewModel = Create();

        await viewModel.RefreshCommand.ExecuteAsync();

        Assert.Equal("Signed in as CR-101", viewModel.Courier);
    }

    [Fact]
    public async Task Sign_out_clears_the_token_keeps_the_scans_and_opens_sign_in()
    {
        _store.Add("APX-20000001", SyncStatus.Pending, Now);
        using var viewModel = Create();

        await viewModel.SignOutCommand.ExecuteAsync();

        Assert.Null(_tokens.Token);
        Assert.Single(_store.Scans);
        Assert.Equal(["login"], _navigator.Visits);
        Assert.Equal("Not signed in", viewModel.Courier);
    }

    [Fact]
    public void The_page_shows_the_circuit_state_through_the_shared_banner()
    {
        using var viewModel = Create();

        _circuit.Update(CircuitState.Open);

        Assert.True(viewModel.Banner.IsOffline);
        Assert.StartsWith("Open", viewModel.Banner.CircuitStateText, StringComparison.Ordinal);
    }

    [Fact]
    public void Dispose_stops_following_sync_runs()
    {
        var viewModel = Create();
        Assert.True(_sync.HasSubscribers);

        viewModel.Dispose();

        Assert.False(_sync.HasSubscribers);
    }

    private SyncStatusViewModel Create() =>
        new(
            _sync,
            _store,
            _tokens,
            _navigator,
            _dispatcher,
            new FixedTimeProvider(Now),
            new ConnectionBannerViewModel(_circuit, _dispatcher));
}
