using ApexPost.Domain.Common;
using ApexPost.Domain.Tracking;

namespace ApexPost.Tracking.UnitTests.Rules;

/// <summary>
/// The shipment lifecycle as a full 6×6 table: every pair of statuses is listed once with the expected answer.
/// The table is written out by hand (not computed) so a wrong rule cannot also produce the wrong expectation.
/// Week 5 step 03 replaced the earlier class of 22 hand-picked rows (see TESTING-STRATEGY.md section 5).
/// </summary>
[Trait("Category", "Unit")]
public sealed class ShipmentStatusRulesTests
{
    private const ShipmentStatus Created = ShipmentStatus.Created;
    private const ShipmentStatus PickedUp = ShipmentStatus.PickedUp;
    private const ShipmentStatus InTransit = ShipmentStatus.InTransit;
    private const ShipmentStatus OutForDelivery = ShipmentStatus.OutForDelivery;
    private const ShipmentStatus Delivered = ShipmentStatus.Delivered;
    private const ShipmentStatus Failed = ShipmentStatus.Failed;

    /// <summary>All 36 (from, to) pairs. True only for a forward move out of a status that is not final.</summary>
    private static readonly (ShipmentStatus From, ShipmentStatus To, bool Allowed)[] Table =
    [
        (Created, Created, false),
        (Created, PickedUp, true),
        (Created, InTransit, true),
        (Created, OutForDelivery, true),
        (Created, Delivered, true),
        (Created, Failed, true),
        (PickedUp, Created, false),
        (PickedUp, PickedUp, false),
        (PickedUp, InTransit, true),
        (PickedUp, OutForDelivery, true),
        (PickedUp, Delivered, true),
        (PickedUp, Failed, true),
        (InTransit, Created, false),
        (InTransit, PickedUp, false),
        (InTransit, InTransit, false),
        (InTransit, OutForDelivery, true),
        (InTransit, Delivered, true),
        (InTransit, Failed, true),
        (OutForDelivery, Created, false),
        (OutForDelivery, PickedUp, false),
        (OutForDelivery, InTransit, false),
        (OutForDelivery, OutForDelivery, false),
        (OutForDelivery, Delivered, true),
        (OutForDelivery, Failed, true),
        (Delivered, Created, false),
        (Delivered, PickedUp, false),
        (Delivered, InTransit, false),
        (Delivered, OutForDelivery, false),
        (Delivered, Delivered, false),
        (Delivered, Failed, false),
        (Failed, Created, false),
        (Failed, PickedUp, false),
        (Failed, InTransit, false),
        (Failed, OutForDelivery, false),
        (Failed, Delivered, false),
        (Failed, Failed, false),
    ];

    public static TheoryData<ShipmentStatus, ShipmentStatus, bool> LifecycleMatrix
    {
        get
        {
            var data = new TheoryData<ShipmentStatus, ShipmentStatus, bool>();
            foreach (var (from, to, allowed) in Table)
            {
                data.Add(from, to, allowed);
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(LifecycleMatrix))]
    public void CanMove_StatusPairOfTheLifecycleTable_ReturnsTheExpectedAnswer(
        ShipmentStatus from,
        ShipmentStatus to,
        bool expected)
    {
        // Arrange
        var pair = (from, to);

        // Act
        var allowed = ShipmentStatusRules.CanMove(pair.from, pair.to);

        // Assert
        Assert.Equal(expected, allowed);
    }

    [Fact]
    public void LifecycleMatrix_EveryDefinedStatus_ListsEachOf36PairsExactlyOnce()
    {
        // Arrange
        var statuses = Enum.GetValues<ShipmentStatus>();
        var expectedPairs = statuses.SelectMany(from => statuses.Select(to => (from, to))).ToHashSet();

        // Act
        var listedPairs = Table.Select(row => (row.From, row.To)).ToList();

        // Assert
        Assert.Equal(36, listedPairs.Count);
        Assert.Equal(expectedPairs, listedPairs.ToHashSet());
    }

    [Theory]
    [InlineData(ShipmentStatus.Created, (ShipmentStatus)99)]
    [InlineData((ShipmentStatus)0, ShipmentStatus.PickedUp)]
    [InlineData((ShipmentStatus)99, ShipmentStatus.Delivered)]
    public void CanMove_UndefinedStatusOnEitherSide_ReturnsFalse(ShipmentStatus from, ShipmentStatus to)
    {
        // Arrange
        var pair = (from, to);

        // Act
        var allowed = ShipmentStatusRules.CanMove(pair.from, pair.to);

        // Assert
        Assert.False(allowed);
    }

    [Theory]
    [InlineData(ShipmentStatus.Created, false)]
    [InlineData(ShipmentStatus.PickedUp, false)]
    [InlineData(ShipmentStatus.InTransit, false)]
    [InlineData(ShipmentStatus.OutForDelivery, false)]
    [InlineData(ShipmentStatus.Delivered, true)]
    [InlineData(ShipmentStatus.Failed, true)]
    public void IsFinal_EachStatus_IsTrueOnlyForDeliveredAndFailed(ShipmentStatus status, bool expected)
    {
        // Arrange
        var subject = status;

        // Act
        var final = ShipmentStatusRules.IsFinal(subject);

        // Assert
        Assert.Equal(expected, final);
    }

    [Theory]
    [InlineData(ScanType.Pickup, ShipmentStatus.PickedUp)]
    [InlineData(ScanType.InTransit, ShipmentStatus.InTransit)]
    [InlineData(ScanType.Delivered, ShipmentStatus.Delivered)]
    [InlineData(ScanType.Failed, ShipmentStatus.Failed)]
    public void StatusFor_EachScanType_ReturnsItsShipmentStatus(ScanType scanType, ShipmentStatus expected)
    {
        // Arrange
        var type = scanType;

        // Act
        var status = ShipmentStatusRules.StatusFor(type);

        // Assert
        Assert.Equal(expected, status);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(99)]
    public void StatusFor_UndefinedScanType_ThrowsDomainExceptionNamingTheValue(int value)
    {
        // Arrange
        var scanType = (ScanType)value;

        // Act
        var error = Assert.Throws<DomainException>(() => ShipmentStatusRules.StatusFor(scanType));

        // Assert
        Assert.Equal($"Scan type '{value}' is not valid.", error.Message);
    }
}
