using ApexPost.Domain.Common;
using ApexPost.Domain.Tracking;

namespace ApexPost.Tracking.UnitTests.Rules;

public sealed class ShipmentStatusRulesTests
{
    public static TheoryData<ShipmentStatus, ShipmentStatus> ForwardMoves => new()
    {
        { ShipmentStatus.Created, ShipmentStatus.PickedUp },
        { ShipmentStatus.PickedUp, ShipmentStatus.InTransit },
        { ShipmentStatus.InTransit, ShipmentStatus.OutForDelivery },
        { ShipmentStatus.OutForDelivery, ShipmentStatus.Delivered },
        { ShipmentStatus.OutForDelivery, ShipmentStatus.Failed },
        { ShipmentStatus.Created, ShipmentStatus.InTransit },
        { ShipmentStatus.PickedUp, ShipmentStatus.Delivered },
        { ShipmentStatus.InTransit, ShipmentStatus.Delivered },
        { ShipmentStatus.InTransit, ShipmentStatus.Failed },
        { ShipmentStatus.Created, ShipmentStatus.Failed },
    };

    public static TheoryData<ShipmentStatus, ShipmentStatus> RefusedMoves => new()
    {
        { ShipmentStatus.PickedUp, ShipmentStatus.Created },
        { ShipmentStatus.InTransit, ShipmentStatus.PickedUp },
        { ShipmentStatus.OutForDelivery, ShipmentStatus.InTransit },
        { ShipmentStatus.Delivered, ShipmentStatus.Failed },
        { ShipmentStatus.Failed, ShipmentStatus.Delivered },
        { ShipmentStatus.Delivered, ShipmentStatus.OutForDelivery },
        { ShipmentStatus.Failed, ShipmentStatus.Created },
        { ShipmentStatus.InTransit, ShipmentStatus.InTransit },
        { ShipmentStatus.Created, ShipmentStatus.Created },
        { ShipmentStatus.Delivered, ShipmentStatus.Delivered },
        { ShipmentStatus.Created, (ShipmentStatus)99 },
        { (ShipmentStatus)0, ShipmentStatus.PickedUp },
    };

    [Theory]
    [MemberData(nameof(ForwardMoves))]
    public void Forward_moves_are_allowed(ShipmentStatus from, ShipmentStatus to)
    {
        Assert.True(ShipmentStatusRules.CanMove(from, to));
    }

    [Theory]
    [MemberData(nameof(RefusedMoves))]
    public void Backward_same_final_and_unknown_moves_are_refused(ShipmentStatus from, ShipmentStatus to)
    {
        Assert.False(ShipmentStatusRules.CanMove(from, to));
    }

    [Fact]
    public void Nothing_leaves_a_final_status()
    {
        foreach (var final in new[] { ShipmentStatus.Delivered, ShipmentStatus.Failed })
        {
            Assert.All(Enum.GetValues<ShipmentStatus>(), to => Assert.False(ShipmentStatusRules.CanMove(final, to)));
        }
    }

    [Theory]
    [InlineData(ScanType.Pickup, ShipmentStatus.PickedUp)]
    [InlineData(ScanType.InTransit, ShipmentStatus.InTransit)]
    [InlineData(ScanType.Delivered, ShipmentStatus.Delivered)]
    [InlineData(ScanType.Failed, ShipmentStatus.Failed)]
    public void Each_scan_type_maps_to_its_status(ScanType scanType, ShipmentStatus expected)
    {
        Assert.Equal(expected, ShipmentStatusRules.StatusFor(scanType));
    }

    [Fact]
    public void An_unknown_scan_type_has_no_status()
    {
        Assert.Throws<DomainException>(() => ShipmentStatusRules.StatusFor((ScanType)99));
    }
}
