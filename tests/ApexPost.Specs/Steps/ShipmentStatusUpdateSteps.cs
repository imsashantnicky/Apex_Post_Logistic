// Copilot-assisted: drafted by GitHub Copilot, reviewed and edited by a human (see COPILOT-LOG.md).
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
[Scope(Feature = "Shipment status update")]
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
