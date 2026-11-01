using ApexPost.Domain.Catalogue;
using ApexPost.Domain.Common;
using ApexPost.Domain.Transactions;
using ApexPost.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ApexPost.Infrastructure.Persistence.Configurations;

internal sealed class RetailLineConfiguration : IEntityTypeConfiguration<RetailLineItem>
{
    public void Configure(EntityTypeBuilder<RetailLineItem> builder)
    {
        builder.ToTable(
            "TransactionLines",
            table =>
            {
                table.HasCheckConstraint("CK_TransactionLines_Quantity", "[Quantity] > 0");
                table.HasCheckConstraint("CK_TransactionLines_UnitPrice", "[UnitPrice] > 0");
                table.HasCheckConstraint("CK_TransactionLines_TaxCategory", ColumnRules.In<TaxCategory>("TaxCategory"));
            });
        builder.Property(line => line.Sku).HasConversion<SkuConverter>().HasMaxLength(8).IsUnicode(false);
        builder.Property(line => line.Name).HasMaxLength(TextLimits.Name);
        builder.Property(line => line.UnitPrice).HasConversion<MoneyConverter>().HasColumnType(ColumnRules.Money);
        builder.Property(line => line.Quantity);
        builder.Property(line => line.TaxCategory).AsEnumText();
        builder.HasOne<Product>().WithMany().HasForeignKey(line => line.Sku).OnDelete(DeleteBehavior.Restrict);
    }
}
