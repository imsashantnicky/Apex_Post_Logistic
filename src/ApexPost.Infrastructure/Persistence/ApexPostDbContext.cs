using ApexPost.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace ApexPost.Infrastructure.Persistence;

/// <summary>The EF Core model of the SQL Server database; each table is described by its own configuration.</summary>
public sealed class ApexPostDbContext(DbContextOptions<ApexPostDbContext> options) : DbContext(options)
{
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
