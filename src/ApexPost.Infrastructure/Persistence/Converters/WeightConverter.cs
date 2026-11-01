using ApexPost.Domain.Shipping;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace ApexPost.Infrastructure.Persistence.Converters;

/// <summary>Stores a <see cref="Weight"/> as whole grams.</summary>
public sealed class WeightConverter() : ValueConverter<Weight, int>(weight => weight.Grams, grams => new Weight(grams));
