using ApexPost.Domain.Transactions;
using ApexPost.Domain.Verification;
using ApexPost.Infrastructure.Persistence.Converters;
using ApexPost.Infrastructure.Persistence.Records;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ApexPost.Infrastructure.Persistence.Configurations;

internal sealed class IdentityVerificationConfiguration : IEntityTypeConfiguration<IdentityVerificationRecord>
{
    private const int Last4Length = IdentityVerification.VisibleCharacters;

    public void Configure(EntityTypeBuilder<IdentityVerificationRecord> builder)
    {
        builder.ToTable(
            "IdentityVerifications",
            table =>
            {
                table.HasCheckConstraint(
                    "CK_IdentityVerifications_DocumentType",
                    ColumnRules.In<DocumentType>("DocumentType"));
                table.HasCheckConstraint(
                    "CK_IdentityVerifications_Result",
                    ColumnRules.In<VerificationResult>("Result"));
                table.HasCheckConstraint("CK_IdentityVerifications_Last4", $"LEN([DocumentLast4]) = {Last4Length}");
            });
        builder.HasKey(record => record.Id);
        builder.Property(record => record.Id).ValueGeneratedNever();
        builder.Property(record => record.DocumentType).AsEnumText();
        builder.Property(record => record.DocumentLast4).HasMaxLength(Last4Length).IsUnicode(false).IsRequired();
        builder.Property(record => record.Result).AsEnumText();
        builder.Property(record => record.VerifiedAt).HasConversion<UtcDateTimeConverter>()
            .HasColumnType(ColumnRules.UtcTime);
        builder.HasOne<CounterTransaction>().WithMany().HasForeignKey(record => record.TransactionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.AddAuditColumns();
    }
}
