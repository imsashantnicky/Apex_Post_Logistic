using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace ApexPost.Infrastructure.Tracking;

/// <summary>A datetime2 keeps no kind; values read back are marked as the UTC they were stored as.</summary>
public sealed class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
    value => value,
    value => DateTime.SpecifyKind(value, DateTimeKind.Utc));
