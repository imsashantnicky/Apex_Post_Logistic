using ApexPost.Application.Abstractions;
using ApexPost.Domain.Branches;
using ApexPost.Domain.Catalogue;
using ApexPost.Domain.Inventory;
using ApexPost.Domain.Personnel;
using ApexPost.Domain.Shipping;
using ApexPost.Domain.Transactions;

namespace ApexPost.Infrastructure.Seeding;

/// <summary>The ports the seeder writes through, so it never depends on how the data is stored.</summary>
public sealed record SeedTargets(
    IRepository<Branch, BranchCode> Branches,
    IRepository<ShippingZone, string> Zones,
    IRepository<Product, Sku> Products,
    IRepository<StockItem, StockKey> Stock,
    IRepository<Staff, Guid> Staff,
    IRepository<CounterTransaction, Guid> Transactions,
    ICredentialStore Credentials,
    IPasswordHasher Hasher);
