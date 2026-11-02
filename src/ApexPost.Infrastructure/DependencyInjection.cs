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
using ApexPost.Infrastructure.Persistence;
using ApexPost.Infrastructure.Persistence.Interceptors;
using ApexPost.Infrastructure.Persistence.Json;
using ApexPost.Infrastructure.Persistence.Json.Mapping;
using ApexPost.Infrastructure.Persistence.Json.Records;
using ApexPost.Infrastructure.Persistence.Seeding;
using ApexPost.Infrastructure.Repositories;
using ApexPost.Infrastructure.Repositories.Queries;
using ApexPost.Infrastructure.Repositories.Sql;
using ApexPost.Infrastructure.Security;
using ApexPost.Infrastructure.Seeding;
using ApexPost.Infrastructure.Shipping;
using ApexPost.Infrastructure.Verification;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace ApexPost.Infrastructure;

/// <summary>
/// The registration points of this layer. All storage sits in one call, <see cref="AddSqlPersistence"/>, and the
/// application runs on it. <see cref="AddJsonPersistence"/> is the Week 2 file storage, which the web host no longer
/// uses; everything stateful there is a singleton.
/// </summary>
public static class DependencyInjection
{
    public const string SqlConnectionName = "ApexPostDb";

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
        services.AddSingleton<CachedCatalog>(provider => new CachedCatalog(
            provider.GetRequiredService<IRepository<Product, Sku>>(),
            provider.GetRequiredService<IRepository<ShippingZone, string>>(),
            provider.GetRequiredService<IClock>(),
            provider.GetRequiredService<IOptions<CatalogSettings>>()));
        services.AddSingleton<IProductCatalog>(provider => provider.GetRequiredService<CachedCatalog>());
        services.AddSingleton<IZoneCatalog>(provider => provider.GetRequiredService<CachedCatalog>());
        services.AddScoped<ITransactionQueries, RepositoryTransactionQueries>();
        services.AddScoped<IReportQueries, RepositoryReportQueries>();
        services.AddSingleton<SeedTargets>();
        services.AddSingleton<DemoDataSeeder>();
        return services;
    }

    /// <summary>
    /// SQL Server storage. The connection string comes from user-secrets or the environment, never from source control.
    /// It is read when a context is built, so nothing connects until SQL Server is used. SQL text is logged only where
    /// the Logging section allows it (Information in Development, Warning otherwise). The factory also registers the
    /// scoped context: writes share it for one unit of work, reads and the audit log get short-lived ones.
    /// </summary>
    public static IServiceCollection AddSqlPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        services.Configure<CatalogSettings>(configuration.GetSection(CatalogSettings.SectionName));

        services.AddScoped<AuditColumnsInterceptor>();
        services.AddScoped<DevelopmentSeeder>();
        services.AddDbContextFactory<ApexPostDbContext>(
            (provider, options) => options
                .UseSqlServer(
                    configuration.GetConnectionString(SqlConnectionName),
                    sql => sql.EnableRetryOnFailure())
                .AddInterceptors(provider.GetRequiredService<AuditColumnsInterceptor>())
                .EnableSensitiveDataLogging(false),
            ServiceLifetime.Scoped);

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IBranchRepository, BranchRepository>();
        services.AddScoped<IStaffRepository, StaffRepository>();
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IStockRepository, StockRepository>();
        services.AddScoped<IStockAdjuster, SqlStockAdjuster>();
        services.AddScoped<ICounterTransactionRepository, CounterTransactionRepository>();
        services.AddScoped<IIdentityVerificationRepository, IdentityVerificationRepository>();

        services.AddScoped<IBranchQueries, SqlBranchQueries>();
        services.AddScoped<IStaffQueries, SqlStaffQueries>();
        services.AddScoped<ICatalogueQueries, SqlCatalogueQueries>();
        services.AddScoped<ITransactionQueries, SqlTransactionQueries>();
        services.AddScoped<IReportQueries, SqlReportQueries>();

        services.AddScoped<IAuditStore, SqlAuditStore>();
        services.AddScoped<ICredentialStore, SqlCredentialStore>();
        services.AddScoped<IDataStoreHealth, SqlDataStoreHealth>();
        services.AddSingleton<SqlCatalogLoader>();
        services.AddSingleton<CachedCatalog>(provider => new CachedCatalog(
            provider.GetRequiredService<SqlCatalogLoader>(),
            provider.GetRequiredService<IClock>(),
            provider.GetRequiredService<IOptions<CatalogSettings>>()));
        services.AddSingleton<IProductCatalog>(provider => provider.GetRequiredService<CachedCatalog>());
        services.AddSingleton<IZoneCatalog>(provider => provider.GetRequiredService<CachedCatalog>());
        return services;
    }

    /// <summary>What stays when the storage changes: clock, security, simulated partners and seed rules.</summary>
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
        services.AddSingleton<SeedRules>();
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
