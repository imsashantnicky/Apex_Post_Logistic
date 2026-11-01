using ApexPost.Domain.Catalogue;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace ApexPost.Infrastructure.Persistence.Converters;

/// <summary>Stores a <see cref="Sku"/> as its text, for example PKG-0001.</summary>
public sealed class SkuConverter() : ValueConverter<Sku, string>(sku => sku.Value, value => new Sku(value));
