using ApexPost.Domain.Common;
using ApexPost.Domain.Tracking;
using ApexPost.Tracking.UnitTests.Support;

namespace ApexPost.Tracking.UnitTests.Domain;

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

    [Theory]
    [InlineData("APX-1234567")]
    [InlineData("APX-123456789")]
    [InlineData("AP-12345678")]
    [InlineData("apx-12345678")]
    [InlineData("APX-1234567A")]
    [InlineData("APX-١٢٣٤٥٦٧٨")]
    [InlineData("")]
    public void A_wrong_tracking_number_is_rejected(string trackingNumber)
    {
        Assert.Throws<DomainException>(() =>
            TrackedShipment.Create(trackingNumber, TestData.Recipient(), 1m, TestData.Now));
    }

    [Theory]
    [InlineData(0.001)]
    [InlineData(30)]
    public void Weights_at_the_limits_are_accepted(double weightKg)
    {
        var shipment = TrackedShipment.Create(
            TestData.TrackingNumber,
            TestData.Recipient(),
            (decimal)weightKg,
            TestData.Now);

        Assert.Equal((decimal)weightKg, shipment.WeightKg);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(30.001)]
    [InlineData(1.0005)]
    public void Weights_outside_the_limits_are_rejected(double weightKg)
    {
        Assert.Throws<DomainException>(() =>
            TrackedShipment.Create(TestData.TrackingNumber, TestData.Recipient(), (decimal)weightKg, TestData.Now));
    }

    [Fact]
    public void A_local_creation_time_is_rejected()
    {
        var local = DateTime.SpecifyKind(TestData.Now, DateTimeKind.Local);

        Assert.Throws<DomainException>(() =>
            TrackedShipment.Create(TestData.TrackingNumber, TestData.Recipient(), 1m, local));
    }

    [Fact]
    public void MoveTo_applies_a_forward_move_and_refuses_a_backward_one()
    {
        var shipment = TestData.Shipment();

        shipment.MoveTo(ShipmentStatus.InTransit);

        Assert.Equal(ShipmentStatus.InTransit, shipment.Status);
        Assert.Throws<DomainException>(() => shipment.MoveTo(ShipmentStatus.PickedUp));
        Assert.Equal(ShipmentStatus.InTransit, shipment.Status);
    }

    [Fact]
    public void A_final_shipment_cannot_be_replaced()
    {
        var shipment = TestData.Shipment();
        shipment.MoveTo(ShipmentStatus.Delivered);

        Assert.Throws<DomainException>(() => shipment.Replace(TestData.Recipient(), 1m));
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
