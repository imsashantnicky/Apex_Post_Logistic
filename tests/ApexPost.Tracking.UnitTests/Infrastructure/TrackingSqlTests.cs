using ApexPost.Application.Exceptions;
using ApexPost.Application.Tracking;
using ApexPost.Domain.Tracking;
using ApexPost.Infrastructure.Tracking;
using ApexPost.Tracking.UnitTests.Support;
using Microsoft.EntityFrameworkCore;

namespace ApexPost.Tracking.UnitTests.Infrastructure;

/// <summary>The service on SQL Server: migrations, query translation, unique indexes and the version check.</summary>
public sealed class TrackingSqlTests(TrackingDatabase database) : IClassFixture<TrackingDatabase>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_shipment_and_its_scans_round_trip_through_SQL_Server()
    {
        var trackingNumber = NewTrackingNumber();
        ScanUpload[] batch =
        [
            TestData.Scan(ScanType.Pickup, trackingNumber),
            TestData.Scan(ScanType.InTransit, trackingNumber, minutesAfterNow: 15),
        ];

        await using (var context = database.NewContext())
        {
            var service = Service(context);
            await service.CreateAsync(trackingNumber, TestData.Command(), Ct);
            Assert.Equal(2, (await service.UploadScansAsync(batch, Ct)).Accepted);
        }

        await using (var context = database.NewContext())
        {
            var service = Service(context);
            var view = await service.GetAsync(trackingNumber, Ct);
            var again = await service.UploadScansAsync(batch, Ct);
            var page = await service.ListAsync(
                1,
                TrackingLimits.MaximumPageSize,
                ShipmentStatus.InTransit,
                "HARBOUR POINT",
                Ct);

            Assert.Equal(ShipmentStatus.InTransit, view.Status);
            Assert.Equal(TrackedShipment.FirstVersion + 1, view.Version);
            Assert.Equal(batch[1].ScanId, view.LatestScan?.ScanId);
            Assert.Equal(DateTimeKind.Utc, view.LatestScan?.ScannedAtUtc.Kind);
            Assert.Equal(2, again.DuplicatesSkipped);
            Assert.Contains(page.Items, item => item.TrackingNumber == trackingNumber);
            Assert.Equal(2, (await service.GetScansAsync(trackingNumber, Ct)).Count);
        }
    }

    [Fact]
    public async Task The_unique_tracking_number_index_becomes_DuplicateKeyException()
    {
        var trackingNumber = NewTrackingNumber();
        await using var first = database.NewContext();
        await using var second = database.NewContext();
        first.Shipments.Add(TestData.Shipment(trackingNumber));
        second.Shipments.Add(TestData.Shipment(trackingNumber));
        await new TrackingUnitOfWork(first).SaveChangesAsync(Ct);

        await Assert.ThrowsAsync<DuplicateKeyException>(() => new TrackingUnitOfWork(second).SaveChangesAsync(Ct));
    }

    [Fact]
    public async Task A_stale_shipment_version_becomes_ConcurrencyConflictException()
    {
        var trackingNumber = NewTrackingNumber();
        await using (var setup = database.NewContext())
        {
            await Service(setup).CreateAsync(trackingNumber, TestData.Command(), Ct);
        }

        await using var first = database.NewContext();
        await using var second = database.NewContext();
        var mine = await first.Shipments.SingleAsync(shipment => shipment.TrackingNumber == trackingNumber, Ct);
        var theirs = await second.Shipments.SingleAsync(shipment => shipment.TrackingNumber == trackingNumber, Ct);
        theirs.MoveTo(ShipmentStatus.PickedUp);
        await new TrackingUnitOfWork(second).SaveChangesAsync(Ct);
        mine.MoveTo(ShipmentStatus.InTransit);

        await Assert.ThrowsAsync<ConcurrencyConflictException>(() =>
            new TrackingUnitOfWork(first).SaveChangesAsync(Ct));
    }

    [Fact]
    public async Task Only_the_tracking_history_table_is_written()
    {
        await using var context = database.NewContext();

        var applied = await context.Database.GetAppliedMigrationsAsync(Ct);
        var weekThreeHistory = await context.Database
            .SqlQuery<int>($"SELECT COUNT(*) AS [Value] FROM sys.tables WHERE name = '__EFMigrationsHistory'")
            .SingleAsync(Ct);

        Assert.Equal(context.Database.GetMigrations(), applied);
        Assert.Equal(0, weekThreeHistory);
    }

    private static ShipmentTrackingService Service(TrackingDbContext context) =>
        new(new TrackedShipmentRepository(context), new SqlTrackingQueries(context), new TrackingUnitOfWork(context),
            new FixedCourier(), new FixedTimeProvider());

    private static string NewTrackingNumber() =>
        "APX-" + Random.Shared.Next(10_000_000, 99_999_999).ToString(System.Globalization.CultureInfo.InvariantCulture);
}
