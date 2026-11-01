using ApexPost.Application.Abstractions;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ApexPost.Web.Health;

/// <summary>Reports the application healthy while the data store accepts writes.</summary>
public sealed class DataStoreHealthCheck(IDataStoreHealth dataStore) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default) =>
        await dataStore.CanWriteAsync(cancellationToken)
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy();
}
