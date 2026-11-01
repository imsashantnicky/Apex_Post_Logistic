using ApexPost.Application.Abstractions;
using ApexPost.Application.DTOs;
using ApexPost.Application.Exceptions;
using ApexPost.Application.Extensions;
using ApexPost.Application.Validation;
using ApexPost.Domain.Catalogue;
using ApexPost.Domain.Events;
using ApexPost.Domain.Inventory;
using ApexPost.Domain.Personnel;

namespace ApexPost.Application.Services;

/// <summary>Manual stock corrections with a reason, by the Branch Manager of the branch.</summary>
public sealed class StockService(
    ICurrentUser user,
    IProductCatalog products,
    StockLedger ledger,
    StockGate gate,
    CounterEvents events,
    CheckoutRules rules,
    AttributeModelValidator validator,
    IClock clock)
{
    public async Task<StockAdjustment> AdjustAsync(StockAdjustmentCommand command, CancellationToken cancellationToken)
    {
        validator.EnsureValid(command);
        if (command.Change == 0)
        {
            throw new ValidationFailedException(nameof(command.Change), "Change must not be zero.");
        }

        user.RequireRole(StaffRole.BranchManager);
        var sku = new Sku(command.Sku);
        _ = await products.FindAsync(sku, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException($"Product {sku} does not exist.");

        var item = await gate.RunAsync(
            user.Branch,
            () => ApplyAsync(sku, command.Change, cancellationToken),
            cancellationToken).ConfigureAwait(false);
        if (command.Change < 0 && rules.IsLowStock(item))
        {
            events.Publish([new StockLow(item.Branch, item.Sku, item.Quantity, clock.UtcNow)]);
        }

        return new StockAdjustment(item.Branch, item.Sku, command.Change, item.Quantity, command.Reason.Trim());
    }

    private async Task<StockItem> ApplyAsync(Sku sku, int change, CancellationToken cancellationToken)
    {
        var existing = await ledger.LoadAsync(user.Branch, [sku], cancellationToken).ConfigureAwait(false);
        if (existing is not [var item])
        {
            var created = change > 0
                ? new StockItem(user.Branch, sku, change)
                : throw new InsufficientStockException(sku, -change, 0);
            await ledger.AddAsync(created, cancellationToken).ConfigureAwait(false);
            return created;
        }

        if (change > 0)
        {
            item.Add(change);
        }
        else
        {
            item.Remove(-change);
        }

        await ledger.SaveAsync([item], cancellationToken).ConfigureAwait(false);
        return item;
    }
}
