using ApexPost.Domain.Catalogue;

namespace ApexPost.Application.Abstractions;

/// <summary>Writes to the product list. Sales read products through <see cref="IProductCatalog"/>.</summary>
public interface IProductRepository
{
    /// <summary>
    /// Changes the price of every product of a category (null = all) in one statement and returns how many changed.
    /// The statement runs at once, so call it inside <see cref="IUnitOfWork.ExecuteInTransactionAsync"/>.
    /// </summary>
    Task<int> RevisePricesAsync(
        ProductCategory? category,
        decimal percent,
        DateTimeOffset at,
        CancellationToken cancellationToken);
}
