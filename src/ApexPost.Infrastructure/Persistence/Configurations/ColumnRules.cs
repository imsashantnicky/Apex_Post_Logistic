using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ApexPost.Infrastructure.Persistence.Configurations;

/// <summary>Column rules shared by the configurations: audit columns, enum columns and their CHECK text.</summary>
internal static class ColumnRules
{
    public const int EnumLength = 20;
    public const string Money = "decimal(18,2)";
    public const string UtcTime = "datetime2";
    public const string CreatedAt = "CreatedAtUtc";
    public const string CreatedBy = "CreatedBy";
    public const string ModifiedAt = "ModifiedAtUtc";
    public const string NowSql = "SYSUTCDATETIME()";
    private const int CreatedByLength = 50;

    /// <summary>CreatedAtUtc, CreatedBy and ModifiedAtUtc shadow columns; a prefix keeps split tables apart.</summary>
    public static void AddAuditColumns<TEntity>(this EntityTypeBuilder<TEntity> builder, string prefix = "")
        where TEntity : class
    {
        builder.Property<DateTime>(prefix + CreatedAt)
            .HasColumnName(CreatedAt).HasColumnType(UtcTime).HasDefaultValueSql(NowSql);
        builder.Property<string?>(prefix + CreatedBy)
            .HasColumnName(CreatedBy).HasMaxLength(CreatedByLength).IsUnicode(false);
        builder.Property<DateTime?>(prefix + ModifiedAt).HasColumnName(ModifiedAt).HasColumnType(UtcTime);
    }

    public static void AddAuditColumns<TOwner, TDependent>(
        this OwnedNavigationBuilder<TOwner, TDependent> builder)
        where TOwner : class
        where TDependent : class
    {
        builder.Property<DateTime>(CreatedAt).HasColumnType(UtcTime).HasDefaultValueSql(NowSql);
        builder.Property<string?>(CreatedBy).HasMaxLength(CreatedByLength).IsUnicode(false);
        builder.Property<DateTime?>(ModifiedAt).HasColumnType(UtcTime);
    }

    /// <summary>The text of a CHECK that limits a column to the names of an enum.</summary>
    public static string In<TEnum>(string column)
        where TEnum : struct, Enum =>
        $"[{column}] IN ({string.Join(", ", Enum.GetNames<TEnum>().Select(name => $"'{name}'"))})";

    public static PropertyBuilder<TEnum> AsEnumText<TEnum>(this PropertyBuilder<TEnum> builder)
        where TEnum : struct, Enum =>
        builder.HasConversion<string>().HasMaxLength(EnumLength).IsUnicode(false);
}
