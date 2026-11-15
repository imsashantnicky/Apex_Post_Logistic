using ApexPost.Application.Tracking;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ApexPost.Infrastructure.Tracking;

/// <summary>
/// The one registration point of shipment tracking. It is additive: it uses the same ApexPostDb connection string
/// as <see cref="DependencyInjection.AddSqlPersistence"/>, but registers only its own context, ports and service.
/// </summary>
public static class TrackingDependencyInjection
{
    /// <summary>The connection string name, the same database as Week 3.</summary>
    public const string ConnectionName = DependencyInjection.SqlConnectionName;

    /// <summary>
    /// Registers <see cref="TrackingDbContext"/>, its repository, queries and unit of work (all scoped),
    /// <see cref="IShipmentTrackingService"/> and the singleton <see cref="ScanAgePolicy"/> (its
    /// <see cref="TrackingOptions"/> keep their defaults unless the host binds them). The connection string is read
    /// when a context is built, from user-secrets or the environment.
    /// </summary>
    public static IServiceCollection AddShipmentTracking(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        services.TryAddSingleton(TimeProvider.System);
        services.AddOptions<TrackingOptions>();
        services.TryAddSingleton<ScanAgePolicy>();
        services.AddDbContext<TrackingDbContext>(options =>
            UseTrackingSqlServer(options, configuration.GetConnectionString(ConnectionName)));
        services.AddScoped<ITrackedShipmentRepository, TrackedShipmentRepository>();
        services.AddScoped<ITrackingQueries, SqlTrackingQueries>();
        services.AddScoped<ITrackingUnitOfWork, TrackingUnitOfWork>();
        services.AddScoped<IShipmentTrackingService, ShipmentTrackingService>();
        return services;
    }

    /// <summary>SQL Server with retries and the tracking history table; used by the host and by dotnet-ef.</summary>
    public static DbContextOptionsBuilder UseTrackingSqlServer(
        DbContextOptionsBuilder options,
        string? connectionString)
    {
        ArgumentNullException.ThrowIfNull(options);
        return options.UseSqlServer(
            connectionString,
            sql => sql
                .EnableRetryOnFailure()
                .MigrationsHistoryTable(TrackingDbContext.MigrationsHistoryTable)
                .MigrationsAssembly(typeof(TrackingDbContext).Assembly.GetName().Name));
    }
}
