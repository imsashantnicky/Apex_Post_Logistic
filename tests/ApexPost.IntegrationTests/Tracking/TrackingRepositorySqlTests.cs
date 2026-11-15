using System.Globalization;
using ApexPost.Application.Exceptions;
using ApexPost.Application.Tracking;
using ApexPost.Domain.Tracking;
using ApexPost.Infrastructure.Tracking;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace ApexPost.IntegrationTests.Tracking;

/// <summary>
/// The tracking repository, queries and unit of work on a throw-away SQL Server database (never a mocked
/// DbContext). Each test proves a rule that only the database enforces: the unique indexes, the WHERE / OFFSET of a
/// filtered page and the Version check in the UPDATE. Two contexts stand for two parallel requests.
/// </summary>
[Trait("Category", "Integration")]
public sealed class TrackingRepositorySqlTests(TrackingDatabase database) : IClassFixture<TrackingDatabase>
{
    private const string DuplicateMessage = "A shipment or scan with the same key already exists.";
    private const string ConflictMessage = "The shipment was changed by someone else.";
    private const int UniqueIndexViolation = 2601;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task SaveChangesAsync_SameTrackingNumberAddedByTwoRequests_ThrowsDuplicateKeyExceptionFromUniqueIndex()
    {
        // Arrange
        var trackingNumber = NewTrackingNumber();
        await using var first = database.NewContext();
        await using var second = database.NewContext();
        var firstRepository = new TrackedShipmentRepository(first);
        var secondRepository = new TrackedShipmentRepository(second);
        Assert.Null(await firstRepository.FindAsync(trackingNumber, Ct));
        Assert.Null(await secondRepository.FindAsync(trackingNumber, Ct));
        firstRepository.Add(TrackingTestData.Shipment(trackingNumber));
        secondRepository.Add(TrackingTestData.Shipment(trackingNumber));
        await new TrackingUnitOfWork(first).SaveChangesAsync(Ct);

        // Act
        var save = () => new TrackingUnitOfWork(second).SaveChangesAsync(Ct);

        // Assert
        var error = await Assert.ThrowsAsync<DuplicateKeyException>(save);
        Assert.Equal(DuplicateMessage, error.Message);
        var sqlError = Assert.IsType<SqlException>(error.InnerException?.InnerException);
        Assert.Equal(UniqueIndexViolation, sqlError.Number);
        Assert.Equal(1, await CountShipmentsAsync(trackingNumber));
    }

    [Fact]
    public async Task UploadScansAsync_SameScanIdUploadedInTwoRequests_StoresTheScanOnce()
    {
        // Arrange
        var trackingNumber = NewTrackingNumber();
        var scan = TrackingTestData.Scan(ScanType.Pickup, trackingNumber);
        await using (var setup = database.NewContext())
        {
            var service = Service(setup);
            await service.CreateAsync(trackingNumber, TrackingTestData.Command(), Ct);
            Assert.Equal(1, (await service.UploadScansAsync([scan], Ct)).Accepted);
        }

        await using var retry = database.NewContext();

        // Act
        var result = await Service(retry).UploadScansAsync([scan], Ct);

        // Assert
        Assert.Equal(0, result.Accepted);
        Assert.Equal(1, result.DuplicatesSkipped);
        Assert.Empty(result.Rejected);
        Assert.Equal(1, await CountScansAsync(scan.ScanId));
    }

    [Fact]
    public async Task SaveChangesAsync_SameScanIdStagedByTwoRequests_SecondThrowsDuplicateKeyAndOneRowStays()
    {
        // Arrange
        var trackingNumber = NewTrackingNumber();
        var scanId = TrackingFormats.ScanIdPrefix + Guid.NewGuid().ToString("D");
        await using (var setup = database.NewContext())
        {
            new TrackedShipmentRepository(setup).Add(TrackingTestData.Shipment(trackingNumber));
            await new TrackingUnitOfWork(setup).SaveChangesAsync(Ct);
        }

        await using var first = database.NewContext();
        await using var second = database.NewContext();
        await StageScanAsync(first, trackingNumber, scanId);
        await StageScanAsync(second, trackingNumber, scanId);
        await new TrackingUnitOfWork(first).SaveChangesAsync(Ct);

        // Act
        var save = () => new TrackingUnitOfWork(second).SaveChangesAsync(Ct);

        // Assert
        var error = await Assert.ThrowsAsync<DuplicateKeyException>(save);
        Assert.Equal(DuplicateMessage, error.Message);
        Assert.Equal(1, await CountScansAsync(scanId));
        await using var check = database.NewContext();
        Assert.Contains(scanId, await new TrackedShipmentRepository(check).FindStoredScanIdsAsync([scanId], Ct));
    }

    [Fact]
    public async Task ListAsync_StatusCityAndSecondPage_FiltersAndPagesInSqlNotInMemory()
    {
        // Arrange
        var city = "Filterton " + Guid.NewGuid().ToString("N")[..8];
        var numbers = await CreateShipmentsAsync(city, count: 5, pickedUp: 3);
        await CreateShipmentsAsync("Elsewhere " + Guid.NewGuid().ToString("N")[..8], count: 1, pickedUp: 1);
        var capture = new SqlCommandCapture();
        await using var context = database.NewContext(capture);
        var filter = new ShipmentListFilter(Page: 2, PageSize: 2, ShipmentStatus.PickedUp, city.ToUpperInvariant());

        // Act
        var page = await new SqlTrackingQueries(context).ListAsync(filter, Ct);

        // Assert
        Assert.Equal(3, page.TotalCount);
        var only = Assert.Single(page.Items);
        Assert.Equal(numbers[2], only.TrackingNumber);
        var pageQuery = Assert.Single(capture.Commands, sql => sql.Contains("OFFSET", StringComparison.Ordinal));
        Assert.Contains("[RecipientCity] = @", pageQuery, StringComparison.Ordinal);
        Assert.Contains("[Status] = @", pageQuery, StringComparison.Ordinal);
        Assert.Contains("ROWS FETCH NEXT @", pageQuery, StringComparison.Ordinal);
        Assert.Contains(capture.Commands, sql => sql.Contains("COUNT(*)", StringComparison.Ordinal)
            && sql.Contains("[RecipientCity] = @", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SaveChangesAsync_ShipmentChangedByTwoRequests_ThrowsConcurrencyConflictAndKeepsTheFirstChange()
    {
        // Arrange
        var trackingNumber = NewTrackingNumber();
        await using (var setup = database.NewContext())
        {
            new TrackedShipmentRepository(setup).Add(TrackingTestData.Shipment(trackingNumber));
            await new TrackingUnitOfWork(setup).SaveChangesAsync(Ct);
        }

        var capture = new SqlCommandCapture();
        await using var mine = database.NewContext(capture);
        await using var theirs = database.NewContext();
        var myCopy = await new TrackedShipmentRepository(mine).FindAsync(trackingNumber, Ct);
        var theirCopy = await new TrackedShipmentRepository(theirs).FindAsync(trackingNumber, Ct);
        theirCopy!.MoveTo(ShipmentStatus.PickedUp);
        await new TrackingUnitOfWork(theirs).SaveChangesAsync(Ct);
        myCopy!.MoveTo(ShipmentStatus.InTransit);
        capture.Clear();

        // Act
        var save = () => new TrackingUnitOfWork(mine).SaveChangesAsync(Ct);

        // Assert
        var error = await Assert.ThrowsAsync<ConcurrencyConflictException>(save);
        Assert.Equal(ConflictMessage, error.Message);
        Assert.IsType<DbUpdateConcurrencyException>(error.InnerException);
        Assert.Contains(capture.Commands, sql => sql.Contains("UPDATE", StringComparison.Ordinal)
            && sql.Contains("[Version] = @", StringComparison.Ordinal));
        await using var check = database.NewContext();
        var stored = await new SqlTrackingQueries(check).FindAsync(trackingNumber, Ct);
        Assert.Equal(ShipmentStatus.PickedUp, stored?.Status);
        Assert.Equal(TrackedShipment.FirstVersion + 1, stored?.Version);
    }

    private static ShipmentTrackingService Service(TrackingDbContext context) =>
        new(
            new TrackedShipmentRepository(context),
            new SqlTrackingQueries(context),
            new TrackingUnitOfWork(context),
            Mock.Of<ICurrentCourier>(courier => courier.CourierId == TrackingTestData.CourierId),
            new FakeTimeProvider(TrackingTestData.Now));

    private static async Task StageScanAsync(TrackingDbContext context, string trackingNumber, string scanId)
    {
        var repository = new TrackedShipmentRepository(context);
        var shipment = await repository.FindAsync(trackingNumber, Ct);
        Assert.Empty(await repository.FindStoredScanIdsAsync([scanId], Ct));
        var details = new ScanDetails(
            scanId,
            ScanType.Pickup,
            TrackingTestData.CourierId,
            "Central Hub",
            TrackingTestData.Now.UtcDateTime,
            null);
        repository.AddScan(TrackingScan.Record(shipment!, details, TrackingTestData.Now.UtcDateTime));
    }

    /// <summary>Creates shipments with ascending tracking numbers in one city; the first ones are picked up.</summary>
    private async Task<string[]> CreateShipmentsAsync(string city, int count, int pickedUp)
    {
        var first = Random.Shared.Next(10_000_000, 89_999_999);
        var numbers = Enumerable.Range(first, count)
            .Select(number => "APX-" + number.ToString(CultureInfo.InvariantCulture))
            .ToArray();
        await using var context = database.NewContext();
        var service = Service(context);
        foreach (var number in numbers)
        {
            await service.CreateAsync(number, TrackingTestData.Command() with { RecipientCity = city }, Ct);
        }

        foreach (var number in numbers.Take(pickedUp))
        {
            await service.ChangeStatusAsync(number, ShipmentStatus.PickedUp, Ct);
        }

        return numbers;
    }

    private async Task<int> CountShipmentsAsync(string trackingNumber)
    {
        await using var context = database.NewContext();
        return await context.Database
            .SqlQuery<int>($"SELECT COUNT(*) AS [Value] FROM TrackedShipments WHERE TrackingNumber = {trackingNumber}")
            .SingleAsync(Ct);
    }

    private async Task<int> CountScansAsync(string scanId)
    {
        await using var context = database.NewContext();
        return await context.Database
            .SqlQuery<int>($"SELECT COUNT(*) AS [Value] FROM TrackingScans WHERE ScanId = {scanId}")
            .SingleAsync(Ct);
    }

    private static string NewTrackingNumber() =>
        "APX-" + Random.Shared.Next(90_000_000, 99_999_999).ToString(CultureInfo.InvariantCulture);
}
