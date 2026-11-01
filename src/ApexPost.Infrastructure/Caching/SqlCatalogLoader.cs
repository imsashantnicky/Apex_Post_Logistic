using ApexPost.Domain.Catalogue;
using ApexPost.Domain.Shipping;
using ApexPost.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ApexPost.Infrastructure.Caching;

/// <summary>
/// Reads the lists behind the singleton catalogue cache. Contexts are scoped, so each load opens its own scope, and
/// the loaded entities stay untracked because they outlive it.
/// </summary>
public sealed class SqlCatalogLoader(IServiceScopeFactory scopes)
{
    public Task<IReadOnlyList<Product>> ProductsAsync(CancellationToken cancellationToken) =>
        LoadAsync<Product>(cancellationToken);

    public Task<IReadOnlyList<ShippingZone>> ZonesAsync(CancellationToken cancellationToken) =>
        LoadAsync<ShippingZone>(cancellationToken);

    private async Task<IReadOnlyList<T>> LoadAsync<T>(CancellationToken cancellationToken)
        where T : class
    {
        var scope = scopes.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            var contexts = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApexPostDbContext>>();
            var database = await contexts.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
            await using (database.ConfigureAwait(false))
            {
                return await database.Set<T>().AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
