using System.Text.Json.Serialization;

namespace ApexPost.Infrastructure.Persistence.Json.Records;

/// <summary>
/// One basket line. The "kind" property tells which line it is, the same idea as a discriminator column
/// in a table-per-hierarchy mapping.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(RetailLineRecord), "retail")]
[JsonDerivedType(typeof(ShippingLineRecord), "shipping")]
[JsonDerivedType(typeof(MoneyOrderLineRecord), "moneyOrder")]
public abstract record LineItemRecord
{
    public required Guid Id { get; init; }
}
