using ApexPost.Domain.Transactions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ApexPost.Infrastructure.Persistence.Configurations;

/// <summary>
/// The shared part of the three line types. Each line type has its own table (retail items in TransactionLines,
/// shipments in Shipments, money orders in MoneyOrders), so a row never has columns that belong to another type.
/// </summary>
internal sealed class TransactionLineConfiguration : IEntityTypeConfiguration<TransactionLine>
{
    public void Configure(EntityTypeBuilder<TransactionLine> builder)
    {
        builder.UseTpcMappingStrategy();
        builder.HasKey(line => line.Id);
        builder.Property(line => line.Id).ValueGeneratedNever();
        builder.Property<Guid>("TransactionId");
        builder.AddAuditColumns();
    }
}
