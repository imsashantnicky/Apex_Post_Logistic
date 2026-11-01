using ApexPost.Domain.Catalogue;
using ApexPost.Infrastructure.Persistence.Records;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ApexPost.Infrastructure.Persistence.Configurations;

internal sealed class ProductCategoryConfiguration : IEntityTypeConfiguration<ProductCategoryRecord>
{
    public void Configure(EntityTypeBuilder<ProductCategoryRecord> builder)
    {
        builder.ToTable(
            "ProductCategories",
            table => table.HasCheckConstraint("CK_ProductCategories_Name", ColumnRules.In<ProductCategory>("Name")));
        builder.HasKey(category => category.Name);
        builder.Property(category => category.Name).AsEnumText().ValueGeneratedNever();
        builder.AddAuditColumns();
        builder.HasData(
            Enum.GetValues<ProductCategory>().Select(category => new { Name = category, CreatedAtUtc = Seed.At }));
    }
}
