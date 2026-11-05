using ApexPost.Domain.Common;
using ApexPost.Infrastructure.Persistence.Configurations;
using ApexPost.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;

namespace ApexPost.Infrastructure.Persistence;

/// <summary>The EF Core model of the SQL Server database; each table is described by its own configuration.</summary>
public sealed class ApexPostDbContext(DbContextOptions<ApexPostDbContext> options) : DbContext(options)
{
    /// <summary>Lets a set-based update write a computed amount back into a Money column.</summary>
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        ArgumentNullException.ThrowIfNull(configurationBuilder);
        configurationBuilder.DefaultTypeMapping<Money>(money => money
            .HasConversion<MoneyConverter>()
            .HasColumnType(ColumnRules.Money));
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.ApplyConfiguration(new RoleConfiguration());
        modelBuilder.ApplyConfiguration(new ProductCategoryConfiguration());
        modelBuilder.ApplyConfiguration(new BranchConfiguration());
        modelBuilder.ApplyConfiguration(new CounterConfiguration());
        modelBuilder.ApplyConfiguration(new StaffConfiguration());
        modelBuilder.ApplyConfiguration(new ProductConfiguration());
        modelBuilder.ApplyConfiguration(new BranchStockConfiguration());
        modelBuilder.ApplyConfiguration(new StockAdjustmentConfiguration());
        modelBuilder.ApplyConfiguration(new ShippingZoneConfiguration());
        modelBuilder.ApplyConfiguration(new CounterTransactionConfiguration());
        modelBuilder.ApplyConfiguration(new IdentityVerificationConfiguration());
        modelBuilder.ApplyConfiguration(new AuditEntryConfiguration());

        // The line types are a hierarchy: the shared base is configured first, then each table.
        modelBuilder.ApplyConfiguration(new TransactionLineConfiguration());
        modelBuilder.ApplyConfiguration(new RetailLineConfiguration());
        modelBuilder.ApplyConfiguration(new ShipmentConfiguration());
        modelBuilder.ApplyConfiguration(new MoneyOrderConfiguration());
    }
}
