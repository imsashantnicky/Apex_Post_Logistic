Tool: GitHub Copilot Chat in <VS Code or Visual Studio + version>, Ask mode
Model: <model name from the Copilot model picker>
Date: <yyyy-mm-dd>
---
Read 
Completed with input: {
  "path": "E:\\week-3-assignment\\Apex_Post_Logistic\\tests\\ApexPost.Specs\\Features\\ScanSync.feature"
}

Read 
Completed with input: {
  "path": "E:\\week-3-assignment\\Apex_Post_Logistic\\tests\\ApexPost.Specs\\Steps\\ScanSyncSteps.cs"
}

Read 
Completed with input: {
  "path": "E:\\week-3-assignment\\Apex_Post_Logistic\\tests\\ApexPost.Specs\\Contexts\\ScanSyncContext.cs"
}

Read 
Completed with input: {
  "path": "E:\\week-3-assignment\\Apex_Post_Logistic\\tests\\ApexPost.Specs\\Hooks\\ScenarioHooks.cs"
}

Read 
Completed with input: {
  "path": "E:\\week-3-assignment\\Apex_Post_Logistic\\tests\\ApexPost.Specs\\Support\\SpecCourier.cs"
}

Read 
Completed with input: {
  "path": "E:\\week-3-assignment\\Apex_Post_Logistic\\tests\\ApexPost.Tracking.UnitTests\\Support\\InMemoryTracking.cs"
}

Read , lines 1 to 300
Completed with input: {
  "path": "E:\\week-3-assignment\\Apex_Post_Logistic\\src\\ApexPost.Application\\Tracking\\ShipmentTrackingService.cs",
  "view_range": [
    1,
    300
  ]
}

Read 
Completed with input: {
  "path": "E:\\week-3-assignment\\Apex_Post_Logistic\\src\\ApexPost.Domain\\Tracking\\ShipmentStatusRules.cs"
}

Read 
Completed with input: {
  "path": "E:\\week-3-assignment\\Apex_Post_Logistic\\src\\ApexPost.Application\\Tracking\\InvalidStatusTransitionException.cs"
}

Read 
Completed with input: {
  "path": "E:\\week-3-assignment\\Apex_Post_Logistic\\src\\ApexPost.Application\\Tracking\\ShipmentView.cs"
}

Read 
Completed with input: {
  "path": "E:\\week-3-assignment\\Apex_Post_Logistic\\src\\ApexPost.Domain\\Tracking\\TrackedShipment.cs"
}

Read 
Completed with input: {
  "path": "E:\\week-3-assignment\\Apex_Post_Logistic\\src\\ApexPost.Application\\Tracking\\TrackingOptions.cs"
}

### `Features/ShipmentStatusUpdate.feature`

```gherkin
@Spec
Feature: Shipment status update
    A dispatcher moves a shipment through its journey. A shipment only moves forward and never changes after it is
    delivered or failed.

Background:
    Given the dispatcher registers shipment "APX-40000001" for "Ava Example" in "Harbour Point"

Scenario: A shipment moves forward step by step
    When the dispatcher changes shipment "APX-40000001" to "Picked Up"
    And the dispatcher changes shipment "APX-40000001" to "In Transit"
    And the dispatcher changes shipment "APX-40000001" to "Out For Delivery"
    And the dispatcher changes shipment "APX-40000001" to "Delivered"
    Then shipment "APX-40000001" has status "Delivered"

Scenario: A backward move is refused
    Given shipment "APX-40000001" is currently at status "In Transit"
    When the dispatcher changes shipment "APX-40000001" to "Picked Up"
    Then the dispatcher is told that the shipment only moves forward
    And shipment "APX-40000001" has status "In Transit"

Scenario: A delivered shipment cannot change any more
    Given shipment "APX-40000001" is currently at status "Delivered"
    When the dispatcher changes shipment "APX-40000001" to "Failed"
    Then the dispatcher is told that the shipment cannot change any more
    And shipment "APX-40000001" has status "Delivered"

Scenario: An unknown shipment cannot be changed
    When the dispatcher changes shipment "APX-40000009" to "Picked Up"
    Then the dispatcher is told that the shipment was not found

Scenario Outline: A status change follows the shipment journey
    Given shipment "APX-40000001" is currently at status "<from>"
    When the dispatcher changes shipment "APX-40000001" to "<to>"
    Then the change is "<outcome>"
    And shipment "APX-40000001" has status "<result>"

Examples:
    | from       | to         | outcome | result     |
    | Created    | In Transit | allowed | In Transit |
    | In Transit | Picked Up  | refused | In Transit |
    | Picked Up  | Picked Up  | refused | Picked Up  |
    | Failed     | Delivered  | refused | Failed     |
```

### `Contexts/ShipmentStatusContext.cs`

```csharp
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
```

### `Steps/ShipmentStatusUpdateSteps.cs`

```csharp
using ApexPost.Application.Exceptions;
using ApexPost.Application.Tracking;
using ApexPost.Domain.Tracking;
using ApexPost.Specs.Contexts;
using ApexPost.Specs.Support;
using Microsoft.Extensions.Options;
using Reqnroll;
using Xunit;

namespace ApexPost.Specs.Steps;

/// <summary>Exercises shipment status changes through the real tracking service and an in-memory server.</summary>
[Binding]
public sealed class ShipmentStatusUpdateSteps(ShipmentStatusContext context)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private ShipmentTrackingService Service => new(
        context.Server,
        context.Server,
        context.Server,
        new SpecCourier("CR-101"),
        context.Clock,
        new ScanAgePolicy(Options.Create(new TrackingOptions())));

    /// <summary>Registers a shipment for the scenario's recipient and destination.</summary>
    [Given("the dispatcher registers shipment {string} for {string} in {string}")]
    public void GivenTheDispatcherRegistersShipment(string trackingNumber, string recipientName, string city)
    {
        var recipient = new Recipient(
            recipientName,
            "+44 20 7946 0101",
            "12 Quay Street",
            city);
        var shipment = TrackedShipment.Create(
            trackingNumber,
            recipient,
            2.5m,
            context.Clock.GetUtcNow().UtcDateTime);

        context.Server.Store(shipment);
    }

    /// <summary>Sets the registered shipment to a valid starting status for the scenario.</summary>
    [Given("shipment {string} is currently at status {string}")]
    public void GivenShipmentIsCurrentlyAtStatus(string trackingNumber, string statusName)
    {
        var shipment = context.Server.Stored(trackingNumber)
            ?? throw new InvalidOperationException($"Shipment {trackingNumber} was not registered.");

        var status = ToShipmentStatus(statusName);
        if (status == ShipmentStatus.Failed)
        {
            shipment.MoveTo(status);
            return;
        }

        for (var next = (int)ShipmentStatus.PickedUp; next <= (int)status; next++)
        {
            shipment.MoveTo((ShipmentStatus)next);
        }
    }

    /// <summary>Asks the service to change status and records either its returned view or an expected exception.</summary>
    [When("the dispatcher changes shipment {string} to {string}")]
    public async Task WhenTheDispatcherChangesShipment(string trackingNumber, string statusName)
    {
        context.LastShipmentView = null;
        context.LastException = null;

        try
        {
            context.LastShipmentView = await Service.ChangeStatusAsync(
                trackingNumber,
                ToShipmentStatus(statusName),
                Ct);
        }
        catch (InvalidStatusTransitionException exception)
        {
            context.LastException = exception;
        }
        catch (NotFoundException exception)
        {
            context.LastException = exception;
        }
    }

    /// <summary>Checks that the shipment's current status matches the expected business status.</summary>
    [Then("shipment {string} has status {string}")]
    public void ThenShipmentHasStatus(string trackingNumber, string statusName)
    {
        var expectedStatus = ToShipmentStatus(statusName);
        var shipment = context.Server.Stored(trackingNumber);

        Assert.NotNull(shipment);
        Assert.Equal(expectedStatus, shipment.Status);

        if (context.LastShipmentView?.TrackingNumber == trackingNumber)
        {
            Assert.Equal(expectedStatus, context.LastShipmentView.Status);
        }
    }

    /// <summary>Checks that a refused backward move explains the forward-only journey.</summary>
    [Then("the dispatcher is told that the shipment only moves forward")]
    public void ThenTheDispatcherIsToldTheShipmentOnlyMovesForward()
    {
        var exception = Assert.IsType<InvalidStatusTransitionException>(context.LastException);
        Assert.Equal(
            "Shipment APX-40000001 cannot move from InTransit to PickedUp; a shipment only moves forward.",
            exception.Message);
    }

    /// <summary>Checks that a delivered shipment cannot transition to another status.</summary>
    [Then("the dispatcher is told that the shipment cannot change any more")]
    public void ThenTheDispatcherIsToldTheShipmentCannotChangeAnyMore()
    {
        var exception = Assert.IsType<InvalidStatusTransitionException>(context.LastException);
        Assert.Equal(
            "Shipment APX-40000001 cannot move from Delivered to Failed; a shipment only moves forward.",
            exception.Message);
    }

    /// <summary>Checks that an unknown shipment is reported as not found.</summary>
    [Then("the dispatcher is told that the shipment was not found")]
    public void ThenTheDispatcherIsToldTheShipmentWasNotFound()
    {
        var exception = Assert.IsType<NotFoundException>(context.LastException);
        Assert.Equal("Shipment APX-40000009 does not exist.", exception.Message);
    }

    /// <summary>Checks whether the requested transition was allowed or refused.</summary>
    [Then("the change is {string}")]
    public void ThenTheChangeIs(string outcome)
    {
        if (outcome == "allowed")
        {
            Assert.Null(context.LastException);
            Assert.NotNull(context.LastShipmentView);
            return;
        }

        Assert.Equal("refused", outcome);
        Assert.IsType<InvalidStatusTransitionException>(context.LastException);
    }

    private static ShipmentStatus ToShipmentStatus(string statusName) =>
        statusName switch
        {
            "Created" => ShipmentStatus.Created,
            "Picked Up" => ShipmentStatus.PickedUp,
            "In Transit" => ShipmentStatus.InTransit,
            "Out For Delivery" => ShipmentStatus.OutForDelivery,
            "Delivered" => ShipmentStatus.Delivered,
            "Failed" => ShipmentStatus.Failed,
            _ => throw new ArgumentOutOfRangeException(nameof(statusName), statusName, "Unknown shipment status.")
        };
}
```

**Potentially ambiguous step text:** None; the bindings use distinct phrases, and both status-change scenarios share one binding.

**Assumptions:** The outline’s initial statuses are set directly on the registered in-memory shipment to establish each scenario’s starting point. The delivered-shipment scenario attempts a change to `Failed` to demonstrate that final statuses cannot transition.
