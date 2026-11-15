using ApexPost.Domain.Common;
using ApexPost.Domain.Tracking;
using ApexPost.Tracking.UnitTests.Support;

namespace ApexPost.Tracking.UnitTests.Domain;

[Trait("Category", "Unit")]
public sealed class TrackedShipmentTests
{
    [Fact]
    public void A_new_shipment_is_Created_with_the_first_version()
    {
        var shipment = TestData.Shipment();

        Assert.Equal(ShipmentStatus.Created, shipment.Status);
        Assert.Equal(TrackedShipment.FirstVersion, shipment.Version);
        Assert.Equal(TestData.Now, shipment.CreatedAtUtc);
        Assert.NotEqual(Guid.Empty, shipment.Id);
    }

    private const string TrackingNumberMessage = "Tracking number must be APX- followed by 8 digits.";
    private const string WeightMessage = "Weight must be above 0 and at most 30 kg, with at most 3 decimals.";

    /// <summary>
    /// Weights the domain accepts. The domain's lower limit is "above 0" with gram precision, so 0.001 kg is the
    /// lightest; the API contract is stricter (0.1 kg, see ShipmentRequestTests).
    /// </summary>
    public static TheoryData<decimal> AcceptedWeights => new()
    {
        0.001m,
        0.1m,
        2.5m,
        29.999m,
        TrackedShipment.MaximumWeightKg,
    };

    /// <summary>Weights the domain rejects: zero, negative, above 30 kg, or more than 3 decimals.</summary>
    public static TheoryData<decimal> RejectedWeights => new()
    {
        0m,
        -1m,
        -0.001m,
        0.0999m,
        0.0001m,
        30.001m,
        31m,
        1.0005m,
    };

    [Theory]
    [InlineData("APX-10000001")]
    [InlineData("APX-00000000")]
    [InlineData("APX-99999999")]
    public void Create_TrackingNumberInTheFormat_KeepsTheNumber(string trackingNumber)
    {
        // Arrange
        var recipient = TestData.Recipient();

        // Act
        var shipment = TrackedShipment.Create(trackingNumber, recipient, 1m, TestData.Now);

        // Assert
        Assert.Equal(trackingNumber, shipment.TrackingNumber);
    }

    [Theory]
    [InlineData("apx-12345678")]
    [InlineData("Apx-12345678")]
    [InlineData("APX-1234567")]
    [InlineData("APX-123456789")]
    [InlineData("APX12345678")]
    [InlineData("AP-12345678")]
    [InlineData(" APX-12345678")]
    [InlineData("APX-12345678 ")]
    [InlineData("APX-1234 5678")]
    [InlineData("APX-1234567A")]
    [InlineData("APX-١٢٣٤٥٦٧٨")]
    [InlineData("")]
    [InlineData(null)]
    public void Create_TrackingNumberOutsideTheFormat_ThrowsDomainExceptionWithTheFormat(string? trackingNumber)
    {
        // Arrange
        var create = () => TrackedShipment.Create(trackingNumber!, TestData.Recipient(), 1m, TestData.Now);

        // Act
        var error = Assert.Throws<DomainException>(create);

        // Assert
        Assert.Equal(TrackingNumberMessage, error.Message);
    }

    [Theory]
    [MemberData(nameof(AcceptedWeights))]
    public void Create_WeightAboveZeroUpToThirtyKgWithGramPrecision_KeepsTheWeight(decimal weightKg)
    {
        // Arrange
        var recipient = TestData.Recipient();

        // Act
        var shipment = TrackedShipment.Create(TestData.TrackingNumber, recipient, weightKg, TestData.Now);

        // Assert
        Assert.Equal(weightKg, shipment.WeightKg);
    }

    [Theory]
    [MemberData(nameof(RejectedWeights))]
    public void Create_WeightOutsideTheLimitsOrPrecision_ThrowsDomainExceptionWithTheLimits(decimal weightKg)
    {
        // Arrange
        var create = () => TrackedShipment.Create(TestData.TrackingNumber, TestData.Recipient(), weightKg, TestData.Now);

        // Act
        var error = Assert.Throws<DomainException>(create);

        // Assert
        Assert.Equal(WeightMessage, error.Message);
    }

    [Theory]
    [MemberData(nameof(RejectedWeights))]
    public void Replace_WeightOutsideTheLimitsOrPrecision_ThrowsDomainExceptionAndKeepsTheOldWeight(decimal weightKg)
    {
        // Arrange
        var shipment = TestData.Shipment();
        var before = shipment.WeightKg;

        // Act
        var error = Assert.Throws<DomainException>(() => shipment.Replace(TestData.Recipient(), weightKg));

        // Assert
        Assert.Equal(WeightMessage, error.Message);
        Assert.Equal(before, shipment.WeightKg);
    }

    [Fact]
    public void A_local_creation_time_is_rejected()
    {
        var local = DateTime.SpecifyKind(TestData.Now, DateTimeKind.Local);

        Assert.Throws<DomainException>(() =>
            TrackedShipment.Create(TestData.TrackingNumber, TestData.Recipient(), 1m, local));
    }

    [Fact]
    public void MoveTo_ForwardMove_ChangesTheStatus()
    {
        // Arrange
        var shipment = TestData.Shipment();

        // Act
        shipment.MoveTo(ShipmentStatus.InTransit);

        // Assert
        Assert.Equal(ShipmentStatus.InTransit, shipment.Status);
    }

    [Fact]
    public void MoveTo_BackwardMove_ThrowsDomainExceptionAndKeepsTheStatus()
    {
        // Arrange
        var shipment = TestData.Shipment();
        shipment.MoveTo(ShipmentStatus.InTransit);

        // Act
        var error = Assert.Throws<DomainException>(() => shipment.MoveTo(ShipmentStatus.PickedUp));

        // Assert
        Assert.Equal("Shipment APX-10000001 cannot move from InTransit to PickedUp.", error.Message);
        Assert.Equal(ShipmentStatus.InTransit, shipment.Status);
    }

    [Theory]
    [InlineData(ShipmentStatus.Delivered)]
    [InlineData(ShipmentStatus.Failed)]
    public void Replace_FinalShipment_ThrowsDomainExceptionNamingTheStatus(ShipmentStatus final)
    {
        // Arrange
        var shipment = TestData.Shipment();
        shipment.MoveTo(final);

        // Act
        var error = Assert.Throws<DomainException>(() => shipment.Replace(TestData.Recipient(), 1m));

        // Assert
        Assert.Equal($"Shipment APX-10000001 is {final} and can no longer change.", error.Message);
    }

    [Theory]
    [InlineData("", "+44 20 7946 0001", "1 Road", "City")]
    [InlineData("Mia", "phone", "1 Road", "City")]
    [InlineData("Mia", "12345", "1 Road", "City")]
    [InlineData("Mia", "+44 20 7946 0001", " ", "City")]
    [InlineData("Mia", "+44 20 7946 0001", "1 Road", "")]
    public void An_incomplete_recipient_is_rejected(string name, string phone, string address, string city)
    {
        Assert.Throws<DomainException>(() => new Recipient(name, phone, address, city));
    }

    [Fact]
    public void A_recipient_keeps_trimmed_values()
    {
        var recipient = new Recipient(" Mia ", "020-7946-0001", " 1 Road ", " Harbour Point ");

        Assert.Equal("Mia", recipient.Name);
        Assert.Equal("Harbour Point", recipient.City);
    }
}
