using System.Globalization;
using ApexPost.Domain.Catalogue;
using ApexPost.Domain.Common;

namespace ApexPost.Domain.Inventory;

/// <summary>Thrown when a sale or adjustment would take stock below zero.</summary>
public sealed class InsufficientStockException : DomainException
{
    public InsufficientStockException()
    {
    }

    public InsufficientStockException(string message)
        : base(message)
    {
    }

    public InsufficientStockException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public InsufficientStockException(Sku sku, int requested, int available)
        : base(string.Create(
            CultureInfo.InvariantCulture,
            $"Not enough stock of {sku}: {requested} requested, {available} available."))
    {
    }
}
