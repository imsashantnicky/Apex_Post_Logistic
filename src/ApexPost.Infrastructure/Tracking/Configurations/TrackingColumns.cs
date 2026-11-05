using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ApexPost.Infrastructure.Tracking.Configurations;

/// <summary>Column rules of the tracking tables: enums as checked varchar(20) text, times as datetime2.</summary>
internal static class TrackingColumns
{
    public const string UtcTime = "datetime2";
    public const string Weight = "decimal(6,3)";
    private const int EnumLength = 20;

    public static PropertyBuilder<TEnum> AsEnumText<TEnum>(this PropertyBuilder<TEnum> builder)
        where TEnum : struct, Enum =>
        builder.HasConversion<string>().HasMaxLength(EnumLength).IsUnicode(false);

    /// <summary>The text of a CHECK that limits a column to the names of an enum.</summary>
    public static string In<TEnum>(string column)
        where TEnum : struct, Enum =>
        $"[{column}] IN ({string.Join(", ", Enum.GetNames<TEnum>().Select(name => $"'{name}'"))})";
}
