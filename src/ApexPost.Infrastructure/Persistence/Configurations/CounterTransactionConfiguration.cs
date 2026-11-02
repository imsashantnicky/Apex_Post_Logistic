using ApexPost.Domain.Branches;
using ApexPost.Domain.Personnel;
using ApexPost.Domain.Transactions;
using ApexPost.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ApexPost.Infrastructure.Persistence.Configurations;

internal sealed class CounterTransactionConfiguration : IEntityTypeConfiguration<CounterTransaction>
{
    public void Configure(EntityTypeBuilder<CounterTransaction> builder)
    {
        builder.ToTable(
            "CounterTransactions",
            table => table.HasCheckConstraint(
                "CK_CounterTransactions_Status",
                ColumnRules.In<TransactionStatus>("Status")));
        builder.HasKey(transaction => transaction.Id);
        builder.Ignore("Version");
        builder.Ignore(transaction => transaction.Lines);
        builder.Ignore(transaction => transaction.Verifications);

        builder.Property(transaction => transaction.Id).ValueGeneratedNever();
        builder.Property(transaction => transaction.Branch).HasColumnName("BranchCode")
            .HasConversion<BranchCodeConverter>().HasMaxLength(5).IsUnicode(false);
        builder.Property(transaction => transaction.CounterId);
        builder.Property(transaction => transaction.ClerkId);
        builder.Property(transaction => transaction.OpenedAt).HasConversion<UtcDateTimeConverter>()
            .HasColumnType(ColumnRules.UtcTime);
        builder.Property(transaction => transaction.Status).AsEnumText().HasDefaultValue(TransactionStatus.Open)
            .HasSentinel(TransactionStatus.Open);
        builder.Property<byte[]>("RowVersion").IsRowVersion().IsRequired();

        // Workloads W2a and W2c of PERFORMANCE.md. The first also replaces the foreign key index on BranchCode.
        builder.HasIndex(transaction => new { transaction.Branch, transaction.OpenedAt })
            .HasDatabaseName("IX_CounterTransactions_BranchCode_OpenedAt")
            .IncludeProperties(transaction => transaction.Status);
        builder.HasIndex(transaction => transaction.OpenedAt)
            .HasDatabaseName("IX_CounterTransactions_OpenedAt")
            .IncludeProperties(transaction => new { transaction.Branch, transaction.Status });

        builder.HasOne<Branch>().WithMany().HasForeignKey(transaction => transaction.Branch)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Counter>().WithMany().HasForeignKey(transaction => transaction.CounterId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Staff>().WithMany().HasForeignKey(transaction => transaction.ClerkId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany<TransactionLine>("_lines").WithOne().HasForeignKey("TransactionId")
            .OnDelete(DeleteBehavior.Cascade);
        ConfigurePayment(builder);
        ConfigureReversal(builder);
        builder.AddAuditColumns();
    }

    private static void ConfigurePayment(EntityTypeBuilder<CounterTransaction> builder) =>
        builder.OwnsOne(
            transaction => transaction.Payment,
            payment =>
            {
                payment.ToTable(
                    "Payments",
                    table =>
                    {
                        table.HasCheckConstraint("CK_Payments_Method", ColumnRules.In<PaymentMethod>("Method"));
                        table.HasCheckConstraint("CK_Payments_Tax", "[Tax] >= 0");
                    });
                payment.WithOwner().HasForeignKey("TransactionId");
                payment.HasKey("TransactionId");
                payment.Property(value => value.Method).AsEnumText();
                payment.Property(value => value.Tax).HasConversion<MoneyConverter>().HasColumnType(ColumnRules.Money);
                payment.Property(value => value.PaidAt).HasConversion<UtcDateTimeConverter>()
                    .HasColumnType(ColumnRules.UtcTime);
                // Every period report starts here: W1, W3, W4, W5 and W7 of PERFORMANCE.md.
                payment.HasIndex(value => value.PaidAt)
                    .HasDatabaseName("IX_Payments_PaidAt")
                    .IncludeProperties(value => value.Tax);
                payment.AddAuditColumns();
            });

    private static void ConfigureReversal(EntityTypeBuilder<CounterTransaction> builder) =>
        builder.OwnsOne(
            transaction => transaction.Reversal,
            reversal =>
            {
                reversal.Property(value => value.ManagerId).HasColumnName("ReversalManagerId");
                reversal.Property(value => value.Reason).HasColumnName("ReversalReason").HasMaxLength(500);
                reversal.Property(value => value.ReversedAt).HasColumnName("ReversedAt")
                    .HasConversion<UtcDateTimeConverter>().HasColumnType(ColumnRules.UtcTime);
                reversal.HasOne<Staff>().WithMany().HasForeignKey(value => value.ManagerId)
                    .OnDelete(DeleteBehavior.Restrict);
            });
}
