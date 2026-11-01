using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace ApexPost.Infrastructure.Persistence.Converters;

/// <summary>Stores a <see cref="DateTimeOffset"/> as a UTC datetime2 and reads it back with a zero offset.</summary>
public sealed class UtcDateTimeConverter()
    : ValueConverter<DateTimeOffset, DateTime>(
        moment => moment.UtcDateTime,
        stored => new DateTimeOffset(DateTime.SpecifyKind(stored, DateTimeKind.Utc)));
