using System.Globalization;
using ApexPost.Application.Exceptions;
using ApexPost.Application.Tracking;
using ApexPost.Domain.Tracking;
using ApexPost.Infrastructure.Tracking;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace ApexPost.IntegrationTests.Tracking;

/// <summary>
/// The tracking service on SQL Server: migrations, query translation, unique indexes and the version check. Moved
/// here from ApexPost.Tracking.UnitTests in Week 5 step 02 because it opens a real database (integration level).
/// </summary>
[Trait("Category", "Integration")]
public sealed class TrackingSqlTests(TrackingDatabase database) : IClassFixture<TrackingDatabase>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task UploadScansAsync_ShipmentAndScansOnSqlServer_RoundTripAndSkipDuplicates()
    {
        // Arrange
        var trackingNumber = NewTrackingNumber();
        ScanUpload[] batch =
        [
            TrackingTestData.Scan(ScanType.Pickup, trackingNumber),
            TrackingTestData.Scan(ScanType.InTransit, trackingNumber, minutesAfterNow: 15),
        ];
        await using (var context = database.NewContext())
        {
            var service = Service(context);
            await service.CreateAsync(trackingNumber, TrackingTestData.Command(), Ct);
            Assert.Equal(2, (await service.UploadScansAsync(batch, Ct)).Accepted);
        }

        await using (var context = database.NewContext())
        {
            var service = Service(context);

            // Act
            var view = await service.GetAsync(trackingNumber, Ct);
            var again = await service.UploadScansAsync(batch, Ct);
            var page = await service.ListAsync(
                1,
                TrackingLimits.MaximumPageSize,
                ShipmentStatus.InTransit,
                "HARBOUR POINT",
                Ct);

            // Assert
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
    public async Task SaveChangesAsync_SecondShipmentWithSameTrackingNumber_ThrowsDuplicateKeyException()
    {
        // Arrange
        var trackingNumber = NewTrackingNumber();
        await using var first = database.NewContext();
        await using var second = database.NewContext();
        first.Shipments.Add(TrackingTestData.Shipment(trackingNumber));
        second.Shipments.Add(TrackingTestData.Shipment(trackingNumber));
        await new TrackingUnitOfWork(first).SaveChangesAsync(Ct);

        // Act
        var save = () => new TrackingUnitOfWork(second).SaveChangesAsync(Ct);

        // Assert
        await Assert.ThrowsAsync<DuplicateKeyException>(save);
    }

    [Fact]
    public async Task SaveChangesAsync_StaleShipmentVersion_ThrowsConcurrencyConflictException()
    {
        // Arrange
        var trackingNumber = NewTrackingNumber();
        await using (var setup = database.NewContext())
        {
            await Service(setup).CreateAsync(trackingNumber, TrackingTestData.Command(), Ct);
        }

        await using var first = database.NewContext();
        await using var second = database.NewContext();
        var mine = await first.Shipments.SingleAsync(shipment => shipment.TrackingNumber == trackingNumber, Ct);
        var theirs = await second.Shipments.SingleAsync(shipment => shipment.TrackingNumber == trackingNumber, Ct);
        theirs.MoveTo(ShipmentStatus.PickedUp);
        await new TrackingUnitOfWork(second).SaveChangesAsync(Ct);
        mine.MoveTo(ShipmentStatus.InTransit);

        // Act
        var save = () => new TrackingUnitOfWork(first).SaveChangesAsync(Ct);

        // Assert
        await Assert.ThrowsAsync<ConcurrencyConflictException>(save);
    }

    [Fact]
    public async Task MigrateAsync_TrackingMigrations_WriteOnlyTheTrackingHistoryTable()
    {
        // Arrange
        await using var context = database.NewContext();

        // Act
        var applied = await context.Database.GetAppliedMigrationsAsync(Ct);
        var weekThreeHistory = await context.Database
            .SqlQuery<int>($"SELECT COUNT(*) AS [Value] FROM sys.tables WHERE name = '__EFMigrationsHistory'")
            .SingleAsync(Ct);

        // Assert
        Assert.Equal(context.Database.GetMigrations(), applied);
        Assert.Equal(0, weekThreeHistory);
    }

    private static ShipmentTrackingService Service(TrackingDbContext context) =>
        new(
            new TrackedShipmentRepository(context),
            new SqlTrackingQueries(context),
            new TrackingUnitOfWork(context),
            Mock.Of<ICurrentCourier>(courier => courier.CourierId == TrackingTestData.CourierId),
            new FakeTimeProvider(TrackingTestData.Now));

    private static string NewTrackingNumber() =>
        "APX-" + Random.Shared.Next(10_000_000, 99_999_999).ToString(CultureInfo.InvariantCulture);
}
