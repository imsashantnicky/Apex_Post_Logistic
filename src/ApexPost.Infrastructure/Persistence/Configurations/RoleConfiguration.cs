using ApexPost.Domain.Personnel;
using ApexPost.Infrastructure.Persistence.Records;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ApexPost.Infrastructure.Persistence.Configurations;

internal sealed class RoleConfiguration : IEntityTypeConfiguration<RoleRecord>
{
    public void Configure(EntityTypeBuilder<RoleRecord> builder)
    {
        builder.ToTable("Roles", table => table.HasCheckConstraint("CK_Roles_Name", ColumnRules.In<StaffRole>("Name")));
        builder.HasKey(role => role.Name);
        builder.Property(role => role.Name).AsEnumText().ValueGeneratedNever();
        builder.AddAuditColumns();
        builder.HasData(Enum.GetValues<StaffRole>().Select(role => new { Name = role, CreatedAtUtc = Seed.At }));
    }
}
