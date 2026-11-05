using ApexPost.Domain.Catalogue;
using ApexPost.Domain.Common;
using ApexPost.Infrastructure.Persistence.Converters;
using ApexPost.Infrastructure.Persistence.Records;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ApexPost.Infrastructure.Persistence.Configurations;

internal sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable(
            "Products",
            table =>
            {
                table.HasCheckConstraint("CK_Products_UnitPrice", "[UnitPrice] > 0");
                table.HasCheckConstraint("CK_Products_TaxCategory", ColumnRules.In<TaxCategory>("TaxCategory"));
            });
        builder.HasKey(product => product.Id);
        builder.Ignore("Version");
        builder.Ignore(product => product.Sku);

        builder.Property(product => product.Id).HasColumnName("Sku").HasConversion<SkuConverter>()
            .HasMaxLength(8).IsUnicode(false).ValueGeneratedNever();
        builder.Property(product => product.Name).HasMaxLength(TextLimits.Name).IsRequired();
        builder.Property(product => product.Category).AsEnumText();
        builder.Property(product => product.UnitPrice).HasConversion<MoneyConverter>().HasColumnType(ColumnRules.Money);
        builder.Property(product => product.TaxCategory).AsEnumText();

        builder.HasOne<ProductCategoryRecord>().WithMany().HasForeignKey(product => product.Category)
            .OnDelete(DeleteBehavior.Restrict);
        builder.AddAuditColumns();
    }
}
