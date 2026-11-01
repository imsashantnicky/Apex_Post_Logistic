using ApexPost.Domain.Branches;
using ApexPost.Domain.Catalogue;
using ApexPost.Domain.Inventory;
using ApexPost.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ApexPost.Infrastructure.Persistence.Configurations;

internal sealed class BranchStockConfiguration : IEntityTypeConfiguration<StockItem>
{
    public void Configure(EntityTypeBuilder<StockItem> builder)
    {
        builder.ToTable("BranchStock", table => table.HasCheckConstraint("CK_BranchStock_Quantity", "[Quantity] >= 0"));
        builder.Ignore("Version");
        builder.Ignore(item => item.Id);
        builder.HasKey(item => new { item.Branch, item.Sku });

        builder.Property(item => item.Branch).HasColumnName("BranchCode").HasConversion<BranchCodeConverter>()
            .HasMaxLength(5).IsUnicode(false);
        builder.Property(item => item.Sku).HasConversion<SkuConverter>().HasMaxLength(8).IsUnicode(false);
        builder.Property(item => item.Quantity);
        builder.Property<byte[]>("RowVersion").IsRowVersion().IsRequired();

        builder.HasOne<Branch>().WithMany().HasForeignKey(item => item.Branch).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Product>().WithMany().HasForeignKey(item => item.Sku).OnDelete(DeleteBehavior.Restrict);
        builder.AddAuditColumns();
    }
}
