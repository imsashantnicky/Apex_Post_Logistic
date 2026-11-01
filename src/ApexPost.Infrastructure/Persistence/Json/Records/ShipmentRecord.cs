using ApexPost.Domain.Shipping;

namespace ApexPost.Infrastructure.Persistence.Json.Records;

public sealed record ShipmentRecord
{
    public required ShipmentType Type { get; init; }

    public required ServiceLevel Level { get; init; }

    public required int WeightGrams { get; init; }

    public required decimal LengthCm { get; init; }

    public required decimal WidthCm { get; init; }

    public required decimal HeightCm { get; init; }

    public required AddressRecord Recipient { get; init; }
}
