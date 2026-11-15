using System.Globalization;
using ApexPost.Api.Contracts;
using ApexPost.Api.Contracts.V1;
using ApexPost.Api.Tests.Support;
using ApexPost.Domain.Tracking;

namespace ApexPost.Api.Tests.Contracts;

/// <summary>The rules of the create, update and status requests, with their boundaries.</summary>
public sealed class ShipmentRequestTests
{
    [Fact]
    public void A_complete_create_request_is_valid() =>
        Assert.Empty(ContractValidator.Validate(Requests.Create()));

    [Fact]
    public void A_complete_update_request_is_valid() =>
        Assert.Empty(ContractValidator.Validate(Requests.Update()));

    [Theory]
    [InlineData("APX-12345678", true)]
    [InlineData("APX-1234567", false)]
    [InlineData("APX-123456789", false)]
    [InlineData("apx-12345678", false)]
    [InlineData("APX-1234567A", false)]
    [InlineData(" APX-12345678", false)]
    [InlineData("", false)]
    public void Tracking_number_must_be_APX_and_8_digits(string trackingNumber, bool valid)
    {
        var request = Copy(Requests.Create(), trackingNumber: trackingNumber);

        Assert.Equal(valid, !ContractValidator.FailedMembers(request).Contains(nameof(request.TrackingNumber)));
    }

    [Fact]
    public void A_wrong_tracking_number_explains_the_format()
    {
        var results = ContractValidator.Validate(Copy(Requests.Create(), trackingNumber: "APX-1"));

        Assert.Contains(results, result => result.ErrorMessage == ContractRules.TrackingNumberMessage);
    }

    [Theory]
    [InlineData("0", false)]
    [InlineData("0.09", false)]
    [InlineData("0.1", true)]
    [InlineData("30", true)]
    [InlineData("30.001", false)]
    [InlineData("70", false)]
    [InlineData("-1", false)]
    public void Weight_must_be_between_0_1_and_30_kg(string weight, bool valid)
    {
        var request = Copy(Requests.Create(), weightKg: decimal.Parse(weight, CultureInfo.InvariantCulture));
        var update = new UpdateShipmentRequest
        {
            RecipientName = "Ben Sample",
            RecipientPhone = "020 7946 0102",
            RecipientAddress = "4 Mill Lane",
            RecipientCity = "Northgate",
            WeightKg = request.WeightKg,
        };

        Assert.Equal(valid, !ContractValidator.FailedMembers(request).Contains(nameof(request.WeightKg)));
        Assert.Equal(valid, !ContractValidator.FailedMembers(update).Contains(nameof(update.WeightKg)));
    }

    [Theory]
    [InlineData(nameof(CreateShipmentRequest.RecipientName))]
    [InlineData(nameof(CreateShipmentRequest.RecipientPhone))]
    [InlineData(nameof(CreateShipmentRequest.RecipientAddress))]
    [InlineData(nameof(CreateShipmentRequest.RecipientCity))]
    public void Every_recipient_field_is_required(string field)
    {
        var blank = Copy(Requests.Create(), blank: field);

        Assert.Contains(field, ContractValidator.FailedMembers(blank));
    }

    [Theory]
    [InlineData("+44 20 7946 0101", true)]
    [InlineData("1234567", true)]
    [InlineData("123456789012345", true)]
    [InlineData("123456", false)]
    [InlineData("1234567890123456", false)]
    [InlineData("+44-20-7946-0101", false)]
    [InlineData("call 1234567", false)]
    [InlineData("++441234567", false)]
    public void Phone_is_7_to_15_digits_or_spaces_with_an_optional_plus(string phone, bool valid)
    {
        var request = Copy(Requests.Create(), phone: phone);

        var results = ContractValidator.Validate(request);

        Assert.Equal(valid, !results.Any(result => result.MemberNames.Contains(nameof(request.RecipientPhone))));
        if (!valid)
        {
            Assert.Contains(results, result => result.ErrorMessage == ContractRules.PhoneMessage);
        }
    }

    [Fact]
    public void A_recipient_name_longer_than_the_column_is_refused()
    {
        var request = Copy(Requests.Create(), name: new string('a', Recipient.NameLength + 1));

        Assert.Contains(nameof(request.RecipientName), ContractValidator.FailedMembers(request));
    }

    [Fact]
    public void Update_request_applies_the_same_recipient_rules()
    {
        var update = new UpdateShipmentRequest { RecipientPhone = "12", WeightKg = 1m };

        var failed = ContractValidator.FailedMembers(update);

        Assert.Equal(
            ["RecipientAddress", "RecipientCity", "RecipientName", "RecipientPhone"],
            failed.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Status_change_needs_a_status() =>
        Assert.Contains(
            nameof(UpdateStatusRequest.Status),
            ContractValidator.FailedMembers(new UpdateStatusRequest()));

    [Fact]
    public void Status_change_refuses_an_undefined_status() =>
        Assert.Contains(
            nameof(UpdateStatusRequest.Status),
            ContractValidator.FailedMembers(new UpdateStatusRequest { Status = (ShipmentStatus)99 }));

    [Fact]
    public void Status_change_to_a_defined_status_is_valid() =>
        Assert.Empty(ContractValidator.Validate(new UpdateStatusRequest { Status = ShipmentStatus.InTransit }));

    private static CreateShipmentRequest Copy(
        CreateShipmentRequest source,
        string? trackingNumber = null,
        string? name = null,
        string? phone = null,
        decimal? weightKg = null,
        string? blank = null) => new()
    {
        TrackingNumber = trackingNumber ?? source.TrackingNumber,
        RecipientName = blank == nameof(source.RecipientName) ? string.Empty : name ?? source.RecipientName,
        RecipientPhone = blank == nameof(source.RecipientPhone) ? string.Empty : phone ?? source.RecipientPhone,
        RecipientAddress = blank == nameof(source.RecipientAddress) ? string.Empty : source.RecipientAddress,
        RecipientCity = blank == nameof(source.RecipientCity) ? string.Empty : source.RecipientCity,
        WeightKg = weightKg ?? source.WeightKg,
    };
}
