using ApexPost.Courier.Offline;
using ApexPost.Courier.Offline.Scans;
using ApexPost.Specs.Contexts;
using ApexPost.Specs.Support;
using Reqnroll;

namespace ApexPost.Specs.Hooks;

/// <summary>
/// Creates and removes what each scenario needs, so scenarios never share data and can run in any order:
/// Shipment creation gets a new API host with its own SQL Server database; Scan sync gets a new temp folder with a
/// Couchbase Lite database. Each feature's hooks are scoped to that feature.
/// </summary>
[Binding]
public sealed class ScenarioHooks(ShipmentContext shipment, ScanSyncContext scanSync)
{
    private const string ShipmentCreation = "Shipment creation";
    private const string ScanSync = "Scan sync";

    /// <summary>A new API host on a throw-away database; the database is created when the host starts.</summary>
    [BeforeScenario]
    [Scope(Feature = ShipmentCreation)]
    public void CreateApiHost() => shipment.Factory = new SpecApiFactory();

    /// <summary>Disposes the client and the last answer, stops the host and drops its database.</summary>
    [AfterScenario]
    [Scope(Feature = ShipmentCreation)]
    public async Task DisposeApiHostAsync()
    {
        shipment.LastResponse?.Dispose();
        if (shipment.HasClient)
        {
            shipment.Client.Dispose();
        }

        if (shipment.HasFactory)
        {
            await shipment.Factory.DisposeAsync();
        }
    }

    /// <summary>A unique temp folder with a new device database in it.</summary>
    [BeforeScenario]
    [Scope(Feature = ScanSync)]
    public void OpenDeviceDatabase()
    {
        scanSync.Folder = Path.Combine(Path.GetTempPath(), "apexpost-specs", Guid.NewGuid().ToString("N"));
        scanSync.Database = new CourierDatabase(Path.Combine(scanSync.Folder, "db"));
        scanSync.Scans = new CouchbaseScanRepository(scanSync.Database);
    }

    /// <summary>Disposes the outbox and the database, then deletes the temp folder.</summary>
    [AfterScenario]
    [Scope(Feature = ScanSync)]
    public void DeleteDeviceDatabase()
    {
        scanSync.Outbox?.Dispose();
        scanSync.Database?.Dispose();
        if (Directory.Exists(scanSync.Folder))
        {
            Directory.Delete(scanSync.Folder, recursive: true);
        }
    }
}
