using ApexPost.Courier.Domain.Enums;
using ApexPost.Courier.Domain.Scans;
using ApexPost.Courier.Offline.Outbox;
using ApexPost.Domain.Tracking;
using ApexPost.Specs.Contexts;
using ApexPost.Specs.Support;
using Microsoft.Extensions.Logging.Abstractions;
using Reqnroll;
using CourierScanType = ApexPost.Courier.Domain.Enums.ScanType;

namespace ApexPost.Specs.Steps;

/// <summary>
/// Steps of ScanSync.feature at service level: scans are recorded in the real Couchbase Lite store on the device,
/// the real <see cref="OutboxService"/> sends them, and the real tracking service on the server decides. Only the
/// network between them is an in-process adapter, and time moves only when a step says so.
/// </summary>
[Binding]
public sealed class ScanSyncSteps(ScanSyncContext context)
{
    private const string Accepted = "accepted";

    private static readonly TimeSpan MinuteBetweenScans = TimeSpan.FromMinutes(1);

    private static readonly Recipient DefaultRecipient =
        new("Ava Example", "+44 20 7946 0101", "12 Quay Street", "Harbour Point");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Given("courier {string} has shipment {string}")]
    public void GivenCourierHasShipment(string courierId, string trackingNumber)
    {
        context.CourierId = courierId;
        context.Server.Store(TrackedShipment.Create(trackingNumber, DefaultRecipient, 2.5m, context.Clock.GetUtcNow()
            .UtcDateTime));
        context.Uploader = new InProcessShipmentUploader(context.Server, courierId, context.Clock);
        context.Outbox = new OutboxService(
            context.DeviceScans,
            context.DeviceScans,
            context.Uploader,
            NullLogger<OutboxService>.Instance);
    }

    [Given("the courier is offline")]
    public void GivenTheCourierIsOffline() => context.Connection.IsOnline = false;

    [Given("the connection returns")]
    [When("the connection returns")]
    public void TheConnectionReturns() => context.Connection.IsOnline = true;

    [Given(@"^the courier records an? ""(\w+)"" scan for shipment ""(.+)"" at ""(.+)""$")]
    [When(@"^the courier records an? ""(\w+)"" scan for shipment ""(.+)"" at ""(.+)""$")]
    public async Task TheCourierRecordsAScan(CourierScanType scanType, string trackingNumber, string location)
    {
        // A minute between scans keeps them in the order they were recorded; the age is counted from the scan.
        context.Clock.Advance(MinuteBetweenScans);
        var scan = ShipmentScan.CreateNew(trackingNumber, scanType, context.CourierId, location, null, context.Clock);
        await context.DeviceScans.CreateAsync(scan, Ct);

        // Every scan is saved on the device first, whatever the connection.
        Assert.Equal(SyncStatus.Pending, scan.SyncStatus);
    }

    [Given("the answer to the next sync is lost on the way back")]
    public void GivenTheAnswerToTheNextSyncIsLost() => context.Connection.LoseNextAnswer = true;

    [Given("{int} hours pass before the connection returns")]
    public void GivenHoursPassBeforeTheConnectionReturns(int hours)
    {
        context.Clock.Advance(TimeSpan.FromHours(hours));
        context.Connection.IsOnline = true;
    }

    [When("the device tries to sync")]
    [When("the device syncs")]
    public async Task TheDeviceSyncs() => context.LastSync = await context.DeviceOutbox.ProcessPendingAsync(ct: Ct);

    [Then("{int} scan(s) is/are waiting to be sent")]
    public async Task ThenScansAreWaitingToBeSent(int count) =>
        Assert.Equal(count, await context.DeviceScans.CountPendingAsync(Ct));

    [Then("every scan on the device is {string}")]
    public async Task ThenEveryScanOnTheDeviceIs(SyncStatus status)
    {
        var scans = await context.DeviceScans.ListAllAsync(Ct);
        Assert.NotEmpty(scans);
        Assert.All(scans, scan => Assert.Equal(status, scan.SyncStatus));
    }

    [Then("the scan for shipment {string} is {string}")]
    public async Task ThenTheScanForShipmentIs(string trackingNumber, SyncStatus status)
    {
        var scan = Assert.Single(await context.DeviceScans.GetByTrackingNumberAsync(trackingNumber, Ct));
        Assert.Equal(status, scan.SyncStatus);
    }

    [Then("the server holds {int} scan(s) for shipment {string}")]
    public void ThenTheServerHoldsScans(int count, string trackingNumber)
    {
        var shipment = context.Server.Stored(trackingNumber)
            ?? throw new InvalidOperationException($"The server has no shipment {trackingNumber}.");
        Assert.Equal(count, context.Server.StoredScans.Count(scan => scan.ShipmentId == shipment.Id));
    }

    [Then("shipment {string} has status {string}")]
    public void ThenShipmentHasStatus(string trackingNumber, string status)
    {
        var expected = Enum.Parse<ShipmentStatus>(status.Replace(" ", string.Empty, StringComparison.Ordinal));
        Assert.Equal(expected, context.Server.Stored(trackingNumber)?.Status);
    }

    [Then("the server reports {int} duplicate scan(s) skipped")]
    public void ThenTheServerReportsDuplicatesSkipped(int count) =>
        Assert.Equal(count, context.Connection.DuplicatesSkipped);

    [Then("the server answers {string}")]
    public void ThenTheServerAnswers(string answer)
    {
        if (answer == Accepted)
        {
            Assert.Empty(context.Connection.Rejected);
            return;
        }

        var rejected = Assert.Single(context.Connection.Rejected);
        Assert.Equal(answer, rejected.Reason);
    }
}
