using ApexPost.Domain.Common;
using ApexPost.Domain.Transactions;
using ApexPost.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ApexPost.Infrastructure.Persistence.Configurations;

/// <summary>A money order line keeps its own details in the MoneyOrders table, one row per line.</summary>
internal sealed class MoneyOrderConfiguration : IEntityTypeConfiguration<MoneyOrderLineItem>
{
    public void Configure(EntityTypeBuilder<MoneyOrderLineItem> builder)
    {
        builder.ToTable(
            "MoneyOrders",
            table =>
            {
                table.Property(order => order.Id).HasColumnName("TransactionLineId");
                table.HasCheckConstraint("CK_MoneyOrders_Amount", "[Amount] > 0 AND [Fee] >= 0");
            });
        builder.Property(order => order.Amount).HasConversion<MoneyConverter>().HasColumnType(ColumnRules.Money);
        builder.Property(order => order.Fee).HasConversion<MoneyConverter>().HasColumnType(ColumnRules.Money);
        builder.Property(order => order.Sender).HasMaxLength(TextLimits.Name);
        builder.Property(order => order.Recipient).HasMaxLength(TextLimits.Name);
    }
}
