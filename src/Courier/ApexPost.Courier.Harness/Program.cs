using ApexPost.Courier.Harness;
using ApexPost.Courier.Offline;
using ApexPost.Courier.Offline.Outbox;
using ApexPost.Courier.Offline.Scans;

using var stop = new CancellationTokenSource();
Console.CancelKeyPress += (_, args) =>
{
    // Keep the process alive so the using declarations below remove the listener token and close the database.
    args.Cancel = true;
    stop.Cancel();
};

var output = Console.Out;
using var db = new CourierDatabase(logger: new ConsoleLogger<CourierDatabase>(output));
var repository = new CouchbaseScanRepository(db);
using var outbox = new OutboxService(
    repository, repository, new FakeShipmentUploader(), new ConsoleLogger<OutboxService>(output));

var monitor = new ScanChangeMonitor(db, repository, TimeProvider.System, new ConsoleLogger<ScanChangeMonitor>(output));
monitor.Start();

var harness = new CourierHarness(db, repository, outbox, TimeProvider.System, Console.In, output);
await harness.RunAsync(stop.Token);
