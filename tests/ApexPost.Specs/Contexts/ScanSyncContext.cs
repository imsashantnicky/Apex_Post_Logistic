using ApexPost.Courier.Offline;
using ApexPost.Courier.Offline.Outbox;
using ApexPost.Courier.Offline.Scans;
using ApexPost.Specs.Support;
using ApexPost.Tracking.UnitTests.Support;
using Microsoft.Extensions.Time.Testing;

namespace ApexPost.Specs.Contexts;

/// <summary>
/// The state of one Scan sync scenario: the courier device (a real Couchbase Lite database in its own temp folder
/// and the real outbox), the server (the real tracking service on in-memory repositories) and one shared fake clock.
/// Reqnroll creates one instance per scenario and injects it through constructors; the hooks fill and empty it.
/// </summary>
public sealed class ScanSyncContext
{
    /// <summary>The fictional instant the scenario starts at; device and server read the same clock.</summary>
    public static readonly DateTimeOffset Start = new(2026, 11, 5, 8, 0, 0, TimeSpan.Zero);

    /// <summary>The device and server clock. It moves only when a step says time passes.</summary>
    public FakeTimeProvider Clock { get; } = new(Start);

    /// <summary>The courier of the scenario, set by the Background.</summary>
    public string CourierId { get; set; } = string.Empty;

    /// <summary>The scenario's own temp folder, created by the BeforeScenario hook.</summary>
    public string Folder { get; set; } = string.Empty;

    /// <summary>The device database in <see cref="Folder"/>.</summary>
    public CourierDatabase? Database { get; set; }

    /// <summary>The device's scan store on <see cref="Database"/>.</summary>
    public CouchbaseScanRepository? Scans { get; set; }

    /// <summary>The device's outbox, built once the courier is known.</summary>
    public OutboxService? Outbox { get; set; }

    /// <summary>The result of the last sync attempt.</summary>
    public OutboxResult? LastSync { get; set; }

    /// <summary>The server's stored shipments and scans.</summary>
    internal InMemoryTracking Server { get; } = new();

    /// <summary>The device's connection to the server.</summary>
    internal InProcessShipmentUploader? Uploader { get; set; }

    /// <summary>The device scan store, or a clear error when the hook did not run.</summary>
    public CouchbaseScanRepository DeviceScans =>
        Scans ?? throw new InvalidOperationException("The BeforeScenario hook did not open the device database.");

    /// <summary>The device outbox, or a clear error when the Background did not set up the courier.</summary>
    public OutboxService DeviceOutbox =>
        Outbox ?? throw new InvalidOperationException("The courier and shipment were not set up.");

    /// <summary>The connection, or a clear error when the Background did not set up the courier.</summary>
    internal InProcessShipmentUploader Connection =>
        Uploader ?? throw new InvalidOperationException("The courier and shipment were not set up.");
}
