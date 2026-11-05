using ApexPost.Courier.Offline.Scans;
using ApexPost.Courier.Offline.Tests.Support;

namespace ApexPost.Courier.Offline.Tests.Indexes;

public sealed class IndexTests : IDisposable
{
    private readonly TempCourierDatabase _db = new();

    [Fact]
    public void Both_value_indexes_exist_on_the_default_collection()
    {
        var indexes = _db.Database.GetDefaultCollection().GetIndexes();

        Assert.Contains(CourierDatabase.TrackingNumberIndexName, indexes);
        Assert.Contains(CourierDatabase.SyncStatusIndexName, indexes);
    }

    [Fact]
    public void Reopening_does_not_create_the_indexes_twice()
    {
        _db.Database.Dispose();

        using var reopened = new CourierDatabase(_db.DatabaseFolder);

        Assert.Equal(2, reopened.GetDefaultCollection().GetIndexes().Count(name => name.StartsWith("idx_scan_",
            StringComparison.Ordinal)));
    }

    [Theory]
    [InlineData(nameof(CouchbaseScanRepository.GetByTrackingNumberAsync), CourierDatabase.TrackingNumberIndexName)]
    [InlineData(nameof(CouchbaseScanRepository.GetPendingAsync), CourierDatabase.SyncStatusIndexName)]
    public void Explain_shows_the_query_searches_its_index(string method, string indexName)
    {
        var plans = _db.Repository.ExplainQueries();

        Assert.Contains($"SEARCH _ USING INDEX {indexName}", plans[method], StringComparison.Ordinal);
    }

    public void Dispose() => _db.Dispose();
}
