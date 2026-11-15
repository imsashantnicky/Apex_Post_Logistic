using ApexPost.Courier.Offline;
using ApexPost.Courier.Offline.Scans;

namespace ApexPost.Courier.Offline.Tests.Support;

/// <summary>
/// Opens a <see cref="CourierDatabase"/> in a new, unique temp folder for one test and deletes the folder afterwards,
/// so tests never share data and can run in parallel.
/// </summary>
internal sealed class TempCourierDatabase : IDisposable
{
    public TempCourierDatabase()
    {
        Folder = Path.Combine(Path.GetTempPath(), "apexpost-courier-offline-tests", Guid.NewGuid().ToString("N"));
        Database = new CourierDatabase(Path.Combine(Folder, "db"));
        Repository = new CouchbaseScanRepository(Database);
    }

    /// <summary>The test's own folder; the database lives in its <c>db</c> sub-folder.</summary>
    public string Folder { get; }

    public string DatabaseFolder => Database.Directory;

    public CourierDatabase Database { get; }

    public CouchbaseScanRepository Repository { get; }

    public void Dispose()
    {
        Database.Dispose();
        if (Directory.Exists(Folder))
        {
            Directory.Delete(Folder, recursive: true);
        }
    }
}
