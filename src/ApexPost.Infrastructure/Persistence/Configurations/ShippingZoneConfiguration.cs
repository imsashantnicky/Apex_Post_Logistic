using ApexPost.Domain.Common;
using ApexPost.Domain.Shipping;
using ApexPost.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ApexPost.Infrastructure.Persistence.Configurations;

/// <summary>One zone is stored in two tables: ShippingZones (identity) and ShippingRates (the two rates).</summary>
internal sealed class ShippingZoneConfiguration : IEntityTypeConfiguration<ShippingZone>
{
    private const string RatePrefix = "Rate";

    public void Configure(EntityTypeBuilder<ShippingZone> builder)
    {
        builder.ToTable("ShippingZones");
        builder.HasKey(zone => zone.Id);
        builder.Ignore("Version");
        builder.Ignore(zone => zone.Code);

        builder.Property(zone => zone.Id).HasColumnName("Code").HasMaxLength(TextLimits.ZoneCode)
            .IsUnicode(false).ValueGeneratedNever();
        builder.Property(zone => zone.Name).HasMaxLength(TextLimits.Name).IsRequired();
        builder.AddAuditColumns();

        builder.Property(zone => zone.BaseRate).HasConversion<MoneyConverter>().HasColumnType(ColumnRules.Money);
        builder.Property(zone => zone.PerKgRate).HasConversion<MoneyConverter>().HasColumnType(ColumnRules.Money);
        builder.AddAuditColumns(RatePrefix);
        builder.SplitToTable(
            "ShippingRates",
            rates =>
            {
                rates.Property(zone => zone.Id).HasColumnName("ZoneCode");
                rates.Property(zone => zone.BaseRate).HasColumnName("BaseRate");
                rates.Property(zone => zone.PerKgRate).HasColumnName("PerKgRate");
                rates.Property(RatePrefix + ColumnRules.CreatedAt).HasColumnName(ColumnRules.CreatedAt);
                rates.Property(RatePrefix + ColumnRules.CreatedBy).HasColumnName(ColumnRules.CreatedBy);
                rates.Property(RatePrefix + ColumnRules.ModifiedAt).HasColumnName(ColumnRules.ModifiedAt);
            });
    }
}
