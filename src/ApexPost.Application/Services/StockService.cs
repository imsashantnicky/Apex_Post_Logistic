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
    IStockAdjuster adjuster,
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

        var reason = command.Reason.Trim();
        var quantity = await gate.RunAsync(
            user.Branch,
            () => adjuster.AdjustAsync(
                user.Branch,
                sku,
                command.Change,
                reason,
                user.StaffId,
                clock.UtcNow,
                cancellationToken),
            cancellationToken).ConfigureAwait(false);
        var item = new StockItem(user.Branch, sku, quantity);
        if (command.Change < 0 && rules.IsLowStock(item))
        {
            events.Publish([new StockLow(item.Branch, item.Sku, item.Quantity, clock.UtcNow)]);
        }

        return new StockAdjustment(item.Branch, item.Sku, command.Change, item.Quantity, reason);
    }
}
