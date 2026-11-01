using ApexPost.Domain.Branches;
using ApexPost.Domain.Common;
using ApexPost.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ApexPost.Infrastructure.Persistence.Configurations;

internal sealed class BranchConfiguration : IEntityTypeConfiguration<Branch>
{
    public void Configure(EntityTypeBuilder<Branch> builder)
    {
        builder.ToTable("Branches", table => table.HasCheckConstraint("CK_Branches_Hours", "[OpensAt] < [ClosesAt]"));
        builder.HasKey(branch => branch.Id);
        builder.Ignore("Version");
        builder.Ignore(branch => branch.Code);

        builder.Property(branch => branch.Id).HasColumnName("Code").HasConversion<BranchCodeConverter>()
            .HasMaxLength(5).IsUnicode(false).ValueGeneratedNever();
        builder.Property(branch => branch.Name).HasMaxLength(TextLimits.Name).IsRequired();
        builder.Property(branch => branch.Region).HasMaxLength(TextLimits.Name).IsRequired();
        builder.Property(branch => branch.OpensAt).HasColumnType("time");
        builder.Property(branch => branch.ClosesAt).HasColumnType("time");
        builder.Property(branch => branch.IsActive).HasDefaultValue(true);

        builder.HasMany(branch => branch.Counters).WithOne().HasForeignKey("BranchCode")
            .OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(branch => branch.Counters).HasField("_counters");
        builder.AddAuditColumns();
    }
}
