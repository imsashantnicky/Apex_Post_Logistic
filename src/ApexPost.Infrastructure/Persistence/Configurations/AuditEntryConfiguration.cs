using ApexPost.Domain.Personnel;
using ApexPost.Infrastructure.Persistence.Converters;
using ApexPost.Infrastructure.Persistence.Records;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ApexPost.Infrastructure.Persistence.Configurations;

/// <summary>
/// The audit log keeps staff and branch as plain values without foreign keys, so that the history can never be
/// blocked or changed by what happens to staff or branches later.
/// </summary>
internal sealed class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntryRecord>
{
    private const int NameLength = 100;
    private const int CorrelationLength = 64;

    public void Configure(EntityTypeBuilder<AuditEntryRecord> builder)
    {
        builder.ToTable(
            "AuditEntries",
            table =>
            {
                table.HasCheckConstraint(
                    "CK_AuditEntries_Role",
                    $"[Role] IS NULL OR {ColumnRules.In<StaffRole>("Role")}");
                table.HasCheckConstraint("CK_AuditEntries_StatusCode", "[StatusCode] BETWEEN 100 AND 599");
                table.HasCheckConstraint("CK_AuditEntries_DurationMs", "[DurationMs] >= 0");
            });
        builder.HasKey(record => record.Id);
        builder.Property(record => record.At).HasConversion<UtcDateTimeConverter>().HasColumnType(ColumnRules.UtcTime);
        builder.Property(record => record.Event).HasMaxLength(NameLength).IsUnicode(false).IsRequired();
        builder.Property(record => record.Role).HasMaxLength(ColumnRules.EnumLength).IsUnicode(false);
        builder.Property(record => record.BranchCode).HasMaxLength(5).IsUnicode(false);
        builder.Property(record => record.Controller).HasMaxLength(NameLength).IsUnicode(false).IsRequired();
        builder.Property(record => record.Action).HasMaxLength(NameLength).IsUnicode(false).IsRequired();
        builder.Property(record => record.RouteValues).IsRequired();
        builder.Property(record => record.CorrelationId).HasMaxLength(CorrelationLength).IsUnicode(false).IsRequired();
        builder.AddAuditColumns();
    }
}
