using ApexPost.Application.Abstractions;
using ApexPost.Application.Reports;
using ApexPost.Application.Settings;
using ApexPost.Domain.Branches;
using ApexPost.Domain.Catalogue;
using ApexPost.Domain.Common;
using ApexPost.Domain.Inventory;
using ApexPost.Domain.Personnel;
using ApexPost.Domain.Shipping;
using ApexPost.Domain.Transactions;
using ApexPost.Infrastructure.Auditing;
using ApexPost.Infrastructure.Caching;
using ApexPost.Infrastructure.Notifications;
using ApexPost.Infrastructure.Persistence.Json;
using ApexPost.Infrastructure.Persistence.Json.Mapping;
using ApexPost.Infrastructure.Persistence.Json.Records;
using ApexPost.Infrastructure.Security;
using ApexPost.Infrastructure.Seeding;
using ApexPost.Infrastructure.Shipping;
using ApexPost.Infrastructure.Verification;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ApexPost.Infrastructure;

/// <summary>
/// The registration points of this layer. All storage sits in <see cref="AddJsonPersistence"/>, so a future
/// AddSqlPersistence replaces that one line; everything stateful is a singleton.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddJsonPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        services.AddOptions<JsonStorageSettings>()
            .Bind(configuration.GetSection(JsonStorageSettings.SectionName))
            .PostConfigure<IHostEnvironment>((settings, environment) =>
                settings.DataFolder = Path.GetFullPath(settings.DataFolder, environment.ContentRootPath));
        services.Configure<CatalogSettings>(configuration.GetSection(CatalogSettings.SectionName));

        services.AddSingleton<JsonFileStore>();
        services.AddSingleton<IDataStoreHealth, DataFolderHealth>();
        services.AddRepository(DataFiles.Branches, new BranchMapper());
        services.AddRepository(DataFiles.Staff, new StaffMapper());
        services.AddRepository(DataFiles.Products, new ProductMapper());
        services.AddRepository(DataFiles.Stock, new StockItemMapper());
        services.AddRepository(DataFiles.Zones, new ZoneMapper());
        services.AddRepository(DataFiles.Transactions, new TransactionMapper());

        services.AddSingleton<ICredentialStore, JsonCredentialStore>();
        services.AddSingleton<IAuditStore, JsonLinesAuditStore>();
        services.AddSingleton<CachedCatalog>();
        services.AddSingleton<IProductCatalog>(provider => provider.GetRequiredService<CachedCatalog>());
        services.AddSingleton<IZoneCatalog>(provider => provider.GetRequiredService<CachedCatalog>());
        services.AddScoped<ITransactionQueries, RepositoryTransactionQueries>();
        services.AddScoped<IReportQueries, RepositoryReportQueries>();
        return services;
    }

    /// <summary>What stays when the storage changes: the clock, security, simulated partners and the seeder.</summary>
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        services.Configure<PasswordSettings>(configuration.GetSection(PasswordSettings.SectionName));
        services.Configure<CourierSettings>(configuration.GetSection(CourierSettings.SectionName));
        services.Configure<SeedSettings>(configuration.GetSection(SeedSettings.SectionName));

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<ICourierTrackingService, SimulatedCourierService>();
        services.AddSingleton<IIdentityVerificationService, SimulatedIdentityVerificationService>();
        services.AddSingleton<INotificationSink, InMemoryNotificationSink>();
        services.AddSingleton<SeedTargets>();
        services.AddSingleton<DemoDataSeeder>();
        return services;
    }

    private static void AddRepository<TEntity, TId, TRecord>(
        this IServiceCollection services,
        string fileName,
        IRecordMapper<TEntity, TId, TRecord> mapper)
        where TEntity : Entity<TId>
        where TId : notnull
        where TRecord : IVersionedRecord =>
        services.AddSingleton<IRepository<TEntity, TId>>(provider =>
            new JsonRepository<TEntity, TId, TRecord>(provider.GetRequiredService<JsonFileStore>(), fileName, mapper));
}
