// Copilot-assisted: drafted by GitHub Copilot, reviewed and edited by a human (see COPILOT-LOG.md).
using ApexPost.Application.Tracking;
using ApexPost.Tracking.UnitTests.Support;
using Microsoft.Extensions.Time.Testing;

namespace ApexPost.Specs.Contexts;

/// <summary>Holds the shipment, clock and latest service outcome for one shipment-status scenario.</summary>
public sealed class ShipmentStatusContext
{
    /// <summary>Creates a scenario context with a fixed fictional start time.</summary>
    public ShipmentStatusContext()
    {
        Clock = new FakeTimeProvider(new DateTimeOffset(2026, 11, 5, 8, 0, 0, TimeSpan.Zero));
    }

    /// <summary>The clock shared by the scenario's shipment and tracking service.</summary>
    public FakeTimeProvider Clock { get; }

    /// <summary>The last shipment view returned by the tracking service, if a change succeeded.</summary>
    public ShipmentView? LastShipmentView { get; set; }

    /// <summary>The last expected exception caught by a status-change step, if one was thrown.</summary>
    public Exception? LastException { get; set; }

    /// <summary>The in-memory server used by this scenario.</summary>
    internal InMemoryTracking Server { get; } = new();
}
