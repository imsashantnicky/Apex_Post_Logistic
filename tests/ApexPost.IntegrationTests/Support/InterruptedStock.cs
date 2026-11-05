using ApexPost.Application.Abstractions;
using ApexPost.Domain.Branches;
using ApexPost.Domain.Catalogue;
using ApexPost.Domain.Inventory;
using ApexPost.Infrastructure.Repositories;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace ApexPost.IntegrationTests.Support;

/// <summary>
/// Pauses a use case right after it read the stock, the moment a race happens in production, so that a test can
/// decide what the rest of the world does before the commit.
/// </summary>
internal sealed class InterruptedStock(IStockRepository inner, Func<Task> afterRead) : IStockRepository
{
    /// <summary>A second server on the same database: its own singletons, so its own stock gate.</summary>
    public static WebApplicationFactory<Program> Server(ApexFactory factory, Func<Task> afterRead) =>
        factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.AddScoped<StockRepository>();
            services.AddScoped<IStockRepository>(provider =>
                new InterruptedStock(provider.GetRequiredService<StockRepository>(), afterRead));
        }));

    public async Task<IReadOnlyList<StockItem>> GetManyAsync(
        BranchCode branch,
        IEnumerable<Sku> skus,
        CancellationToken cancellationToken)
    {
        var items = await inner.GetManyAsync(branch, skus, cancellationToken);
        await afterRead();
        return items;
    }

    public void Update(IEnumerable<StockItem> items) => inner.Update(items);
}
