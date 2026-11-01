using ApexPost.Domain.Branches;
using ApexPost.Domain.Common;
using ApexPost.Domain.Personnel;
using ApexPost.Infrastructure.Persistence.Converters;
using ApexPost.Infrastructure.Persistence.Records;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ApexPost.Infrastructure.Persistence.Configurations;

internal sealed class StaffConfiguration : IEntityTypeConfiguration<Staff>
{
    public void Configure(EntityTypeBuilder<Staff> builder)
    {
        builder.ToTable("Staff", table => table.HasCheckConstraint("CK_Staff_Role", ColumnRules.In<StaffRole>("Role")));
        builder.HasKey(staff => staff.Id);
        builder.Ignore("Version");
        builder.Ignore(staff => staff.CanSignIn);
        builder.Ignore(staff => staff.CanTransact);

        builder.Property(staff => staff.Id).ValueGeneratedNever();
        builder.Property(staff => staff.Name).HasMaxLength(TextLimits.Name).IsRequired()
            .UsePropertyAccessMode(PropertyAccessMode.Property);
        builder.Property(staff => staff.Email).HasMaxLength(TextLimits.Email).IsUnicode(false).IsRequired()
            .UsePropertyAccessMode(PropertyAccessMode.Property);
        builder.Property(staff => staff.Role).AsEnumText().UsePropertyAccessMode(PropertyAccessMode.Property);
        builder.Property(staff => staff.HomeBranch).HasConversion<BranchCodeConverter>().HasMaxLength(5)
            .IsUnicode(false).UsePropertyAccessMode(PropertyAccessMode.Property);
        builder.Property(staff => staff.IsActive).HasDefaultValue(true);

        builder.HasIndex(staff => staff.Email).IsUnique().HasDatabaseName("UX_Staff_Email");
        builder.HasOne<Branch>().WithMany().HasForeignKey(staff => staff.HomeBranch).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<RoleRecord>().WithMany().HasForeignKey(staff => staff.Role).OnDelete(DeleteBehavior.Restrict);
        builder.AddAuditColumns();
    }
}
