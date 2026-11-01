using ApexPost.Domain.Common;

namespace ApexPost.Domain.Shipping;

/// <summary>Outer package size in centimetres.</summary>
public sealed record Dimensions(decimal LengthCm, decimal WidthCm, decimal HeightCm)
{
    /// <summary>Industry divisor that turns cubic centimetres into volumetric kilograms.</summary>
    public const decimal VolumetricDivisor = 5000m;

    public decimal LengthCm { get; } = Guard.Positive(LengthCm, "Length");

    public decimal WidthCm { get; } = Guard.Positive(WidthCm, "Width");

    public decimal HeightCm { get; } = Guard.Positive(HeightCm, "Height");

    public decimal VolumetricKg => LengthCm * WidthCm * HeightCm / VolumetricDivisor;
}
