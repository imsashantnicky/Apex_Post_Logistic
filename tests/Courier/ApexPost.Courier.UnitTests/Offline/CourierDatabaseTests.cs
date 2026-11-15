using ApexPost.Courier.Offline;
using Couchbase.Lite;

namespace ApexPost.Courier.UnitTests.Offline;

public sealed class CourierDatabaseTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), "apexpost-courier-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void Opening_creates_both_value_indexes()
    {
        using var database = new CourierDatabase(_directory);

        var indexes = database.GetIndexNames();

        Assert.Contains(CourierDatabase.TrackingNumberIndexName, indexes);
        Assert.Contains(CourierDatabase.SyncStatusIndexName, indexes);
    }

    [Fact]
    public void Reopening_the_same_folder_keeps_the_indexes_without_duplicates()
    {
        new CourierDatabase(_directory).Dispose();

        using var reopened = new CourierDatabase(_directory);

        Assert.Equal(2, reopened.GetIndexNames().Count);
    }

    [Fact]
    public void Default_collection_is_returned_while_open()
    {
        using var database = new CourierDatabase(_directory);

        var collection = database.GetDefaultCollection();

        Assert.Equal(0UL, collection.Count);
    }

    [Fact]
    public void Dispose_twice_does_not_throw()
    {
        var database = new CourierDatabase(_directory);

        database.Dispose();
        var error = Record.Exception(database.Dispose);

        Assert.Null(error);
    }

    [Fact]
    public void Use_after_dispose_throws_ObjectDisposedException()
    {
        var database = new CourierDatabase(_directory);
        var token = database.GetDefaultCollection().AddChangeListener((_, _) => { });
        database.Dispose();

        Assert.Throws<ObjectDisposedException>(database.GetDefaultCollection);
        Assert.Throws<ObjectDisposedException>(() => database.RegisterListenerToken(token));
    }

    [Fact]
    public void Dispose_with_registered_listener_tokens_closes_cleanly()
    {
        var database = new CourierDatabase(_directory);
        var collection = database.GetDefaultCollection();
        var calls = 0;
        database.RegisterListenerToken(collection.AddChangeListener((_, _) => Interlocked.Increment(ref calls)));

        var error = Record.Exception(database.Dispose);

        Assert.Null(error);
        Assert.Equal(0, calls);
    }

    public void Dispose()
    {
        if (System.IO.Directory.Exists(_directory))
        {
            System.IO.Directory.Delete(_directory, recursive: true);
        }
    }
}
