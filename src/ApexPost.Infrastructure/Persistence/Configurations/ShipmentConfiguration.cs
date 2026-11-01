using ApexPost.Domain.Common;
using ApexPost.Domain.Rules;
using ApexPost.Domain.Shipping;
using ApexPost.Domain.Transactions;
using ApexPost.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ApexPost.Infrastructure.Persistence.Configurations;

/// <summary>A shipping line is stored in the Shipments table together with its shipment, one row per line.</summary>
internal sealed class ShipmentConfiguration : IEntityTypeConfiguration<ShippingLineItem>
{
    private const string DimensionPrecision = "decimal(9,2)";
    private const string RecipientPrefix = "Recipient";

    public void Configure(EntityTypeBuilder<ShippingLineItem> builder)
    {
        var maxGrams = Weight.FromKilograms(WeightLimitRule.ParcelStandardMaxKg).Grams;
        builder.ToTable(
            "Shipments",
            table =>
            {
                table.Property(line => line.Id).HasColumnName("TransactionLineId");
                table.HasCheckConstraint("CK_Shipments_Price", "[Price] > 0");
                table.HasCheckConstraint("CK_Shipments_Type", ColumnRules.In<ShipmentType>("Type"));
                table.HasCheckConstraint("CK_Shipments_Level", ColumnRules.In<ServiceLevel>("Level"));
                table.HasCheckConstraint("CK_Shipments_Weight", $"[WeightGrams] > 0 AND [WeightGrams] <= {maxGrams}");
            });
        builder.Property(line => line.ZoneCode).HasMaxLength(TextLimits.ZoneCode).IsUnicode(false);
        builder.Property(line => line.Price).HasConversion<MoneyConverter>().HasColumnType(ColumnRules.Money);
        builder.HasOne<ShippingZone>().WithMany().HasForeignKey(line => line.ZoneCode)
            .OnDelete(DeleteBehavior.Restrict);

        builder.OwnsOne(
            line => line.Shipment,
            shipment =>
            {
                shipment.Property(value => value.Type).HasColumnName("Type").AsEnumText();
                shipment.Property(value => value.Level).HasColumnName("Level").AsEnumText();
                shipment.Property(value => value.Weight).HasColumnName("WeightGrams")
                    .HasConversion<WeightConverter>();
                shipment.OwnsOne(value => value.Dimensions, ConfigureDimensions);
                shipment.OwnsOne(value => value.Recipient, ConfigureRecipient);
            });
    }

    private static void ConfigureDimensions(OwnedNavigationBuilder<Shipment, Dimensions> dimensions)
    {
        dimensions.Property(value => value.LengthCm).HasColumnName("LengthCm").HasColumnType(DimensionPrecision);
        dimensions.Property(value => value.WidthCm).HasColumnName("WidthCm").HasColumnType(DimensionPrecision);
        dimensions.Property(value => value.HeightCm).HasColumnName("HeightCm").HasColumnType(DimensionPrecision);
    }

    private static void ConfigureRecipient(OwnedNavigationBuilder<Shipment, Address> address)
    {
        address.Property(value => value.Name).HasColumnName(RecipientPrefix + nameof(Address.Name))
            .HasMaxLength(TextLimits.Name);
        address.Property(value => value.Street).HasColumnName(RecipientPrefix + nameof(Address.Street))
            .HasMaxLength(TextLimits.AddressLine);
        address.Property(value => value.City).HasColumnName(RecipientPrefix + nameof(Address.City))
            .HasMaxLength(TextLimits.Name);
        address.Property(value => value.PostalCode).HasColumnName(RecipientPrefix + nameof(Address.PostalCode))
            .HasMaxLength(TextLimits.PostalCode);
        address.Property(value => value.Country).HasColumnName(RecipientPrefix + nameof(Address.Country))
            .HasMaxLength(TextLimits.Name);
    }
}
