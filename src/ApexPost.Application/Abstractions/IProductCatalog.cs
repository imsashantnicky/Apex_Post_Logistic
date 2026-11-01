using ApexPost.Domain.Catalogue;

namespace ApexPost.Application.Abstractions;

/// <summary>Read-only product lookups; answered from a cache most of the time, hence ValueTask.</summary>
public interface IProductCatalog
{
    ValueTask<Product?> FindAsync(Sku sku, CancellationToken cancellationToken);

    ValueTask<IReadOnlyList<Product>> ListAsync(CancellationToken cancellationToken);
}
