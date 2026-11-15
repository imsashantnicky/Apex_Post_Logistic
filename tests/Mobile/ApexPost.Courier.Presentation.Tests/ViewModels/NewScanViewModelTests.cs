using System.Net;
using ApexPost.Courier.ApiClient.Resilience;
using ApexPost.Courier.ApiClient.Results;
using ApexPost.Courier.Domain.Enums;
using ApexPost.Courier.Presentation.Tests.Support;
using ApexPost.Courier.Presentation.ViewModels;

namespace ApexPost.Courier.Presentation.Tests.ViewModels;

public sealed class NewScanViewModelTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 11, 7, 9, 30, 0, TimeSpan.Zero);

    private readonly InMemoryScanStore _store = new();
    private readonly InMemoryTokenStore _tokens = new(TestApi.CourierToken("CR-101"));
    private readonly FakeNavigator _navigator = new();
    private readonly FakeSyncRunner _sync = new();
    private readonly CannedHandler _handler = new(HttpStatusCode.OK, "{}", TestApi.Json);

    [Fact]
    public async Task A_valid_scan_is_saved_as_pending_with_the_courier_from_the_token()
    {
        var viewModel = Create();
        viewModel.TrackingNumber = " apx-20000001 ";
        viewModel.SelectedScanType = ScanType.InTransit;
        viewModel.Location = " Central Hub ";
        viewModel.Notes = "Left at depot";

        await viewModel.SaveCommand.ExecuteAsync();

        var scan = Assert.Single(_store.Scans);
        Assert.Equal("APX-20000001", scan.TrackingNumber);
        Assert.Equal(ScanType.InTransit, scan.ScanType);
        Assert.Equal("CR-101", scan.CourierId);
        Assert.Equal("Central Hub", scan.Location);
        Assert.Equal(SyncStatus.Pending, scan.SyncStatus);
        Assert.Equal(Now, scan.ScannedAtUtc);
        Assert.StartsWith("scan::", scan.Id, StringComparison.Ordinal);
        Assert.False(viewModel.StatusIsError);
        Assert.Contains("Pending", viewModel.Status, StringComparison.Ordinal);
        Assert.Equal(string.Empty, viewModel.Notes);
        Assert.Equal(["new scan"], _sync.Triggers);
    }

    [Theory]
    [InlineData("")]
    [InlineData("APX-1234567")]
    [InlineData("APX-123456789")]
    [InlineData("ABC-12345678")]
    [InlineData("APX-1234567X")]
    public async Task A_malformed_tracking_number_shows_a_field_error_and_saves_nothing(string trackingNumber)
    {
        var viewModel = Create();
        viewModel.TrackingNumber = trackingNumber;
        viewModel.Location = "Central Hub";

        await viewModel.SaveCommand.ExecuteAsync();

        Assert.Empty(_store.Scans);
        Assert.True(viewModel.HasTrackingNumberError);
        Assert.Equal(FailureMessages.InvalidTrackingNumber, viewModel.TrackingNumberError);
        Assert.False(viewModel.HasLocationError);
        Assert.True(viewModel.StatusIsError);
        Assert.Empty(_sync.Triggers);
    }

    [Fact]
    public async Task A_missing_location_shows_a_field_error()
    {
        var viewModel = Create();
        viewModel.TrackingNumber = "APX-20000001";
        viewModel.Location = "   ";

        await viewModel.SaveCommand.ExecuteAsync();

        Assert.Empty(_store.Scans);
        Assert.Equal(NewScanViewModel.LocationRequired, viewModel.LocationError);
        Assert.False(viewModel.HasTrackingNumberError);
    }

    [Fact]
    public async Task Field_errors_clear_once_the_input_is_fixed()
    {
        var viewModel = Create();
        await viewModel.SaveCommand.ExecuteAsync();
        Assert.True(viewModel.HasTrackingNumberError);
        Assert.True(viewModel.HasLocationError);

        viewModel.TrackingNumber = "APX-20000001";
        viewModel.Location = "Central Hub";
        await viewModel.SaveCommand.ExecuteAsync();

        Assert.False(viewModel.HasTrackingNumberError);
        Assert.False(viewModel.HasLocationError);
        Assert.Single(_store.Scans);
    }

    [Fact]
    public async Task Without_a_courier_token_the_courier_is_sent_to_sign_in()
    {
        _tokens.Clear();
        var viewModel = Create();
        viewModel.TrackingNumber = "APX-20000001";
        viewModel.Location = "Central Hub";

        await viewModel.SaveCommand.ExecuteAsync();

        Assert.Empty(_store.Scans);
        Assert.Equal(["login"], _navigator.Visits);
    }

    [Fact]
    public void Delivered_is_not_offered_until_the_signature_field_exists()
    {
        Assert.Equal([ScanType.Pickup, ScanType.InTransit, ScanType.Failed], Create().ScanTypes);
    }

    public void Dispose() => _handler.Dispose();

    private NewScanViewModel Create()
    {
        var api = TestApi.Client(_handler);
        var login = new LoginViewModel(api, _tokens, _navigator, _sync);
        var banner = new ConnectionBannerViewModel(new CircuitStateTracker(), new InlineDispatcher());
        return new NewScanViewModel(
            _store, _tokens, _navigator, _sync, login, new FixedTimeProvider(Now), new ShipmentLookupViewModel(api), banner);
    }
}
