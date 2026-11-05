using ApexPost.Domain.Branches;
using ApexPost.Domain.Catalogue;
using ApexPost.Domain.Common;
using ApexPost.Domain.Personnel;
using ApexPost.Infrastructure.Persistence.Converters;
using ApexPost.Infrastructure.Persistence.Records;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ApexPost.Infrastructure.Persistence.Configurations;

internal sealed class StockAdjustmentConfiguration : IEntityTypeConfiguration<StockAdjustmentRecord>
{
    public void Configure(EntityTypeBuilder<StockAdjustmentRecord> builder)
    {
        builder.ToTable(
            "StockAdjustments",
            table =>
            {
                table.HasCheckConstraint("CK_StockAdjustments_NewQuantity", "[NewQuantity] >= 0");
                table.HasCheckConstraint("CK_StockAdjustments_Change", "[Change] <> 0");
            });
        builder.HasKey(record => record.Id);
        builder.Property(record => record.Id).ValueGeneratedNever();
        builder.Property(record => record.Branch).HasColumnName("BranchCode").HasConversion<BranchCodeConverter>()
            .HasMaxLength(5).IsUnicode(false);
        builder.Property(record => record.Sku).HasConversion<SkuConverter>().HasMaxLength(8).IsUnicode(false);
        builder.Property(record => record.Reason).HasMaxLength(TextLimits.Reason).IsRequired();
        builder.Property(record => record.AdjustedAt).HasConversion<UtcDateTimeConverter>()
            .HasColumnType(ColumnRules.UtcTime);

        builder.HasOne<Branch>().WithMany().HasForeignKey(record => record.Branch).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Product>().WithMany().HasForeignKey(record => record.Sku).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Staff>().WithMany().HasForeignKey(record => record.StaffId).OnDelete(DeleteBehavior.Restrict);
        builder.AddAuditColumns();
    }
}
