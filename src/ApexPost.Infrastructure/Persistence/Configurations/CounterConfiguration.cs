using ApexPost.Domain.Branches;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ApexPost.Infrastructure.Persistence.Configurations;

internal sealed class CounterConfiguration : IEntityTypeConfiguration<Counter>
{
    public void Configure(EntityTypeBuilder<Counter> builder)
    {
        builder.ToTable("Counters", table => table.HasCheckConstraint("CK_Counters_Number", "[Number] > 0"));
        builder.HasKey(counter => counter.Id);
        builder.Ignore("Version");
        builder.Property(counter => counter.Id).ValueGeneratedNever();
        builder.Property(counter => counter.Number);
        builder.HasIndex("BranchCode", nameof(Counter.Number)).IsUnique().HasDatabaseName("UX_Counters_Branch_Number");
        builder.AddAuditColumns();
    }
}
