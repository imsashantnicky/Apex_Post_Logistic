using ApexPost.Application.Abstractions;
using ApexPost.Application.DTOs;
using ApexPost.Application.Validation;
using ApexPost.Domain.Inventory;
using ApexPost.Domain.Personnel;
using ApexPost.Domain.Transactions;

namespace ApexPost.Application.Services;

/// <summary>
/// Voids and refunds. Only an active Branch Manager of the same branch may reverse a transaction, and a
/// reason is required. Retail goods of a paid transaction go back into stock in the same method.
/// </summary>
public sealed class ReversalService(
    TransactionAccess access,
    StockLedger ledger,
    StockGate gate,
    CounterEvents events,
    AttributeModelValidator validator,
    IClock clock)
{
    private delegate void ReversalStep(
        CounterTransaction transaction,
        Staff manager,
        string reason,
        IReadOnlyCollection<StockItem> stock,
        DateTimeOffset now);

    public Task VoidAsync(Guid id, ReversalCommand command, CancellationToken cancellationToken) =>
        ReverseAsync(
            id,
            command,
            (transaction, manager, reason, stock, now) => transaction.Void(manager, reason, stock, now),
            cancellationToken);

    public Task RefundAsync(Guid id, ReversalCommand command, CancellationToken cancellationToken) =>
        ReverseAsync(
            id,
            command,
            (transaction, manager, reason, stock, now) => transaction.Refund(manager, reason, stock, now),
            cancellationToken);

    private Task ReverseAsync(
        Guid id,
        ReversalCommand command,
        ReversalStep reverse,
        CancellationToken cancellationToken)
    {
        validator.EnsureValid(command);
        return gate.RunAsync(
            access.User.Branch,
            async () =>
            {
                var (transaction, manager) = await access.ForReversalAsync(id, cancellationToken)
                    .ConfigureAwait(false);
                var stock = transaction.Status == TransactionStatus.Paid
                    ? await ledger.LoadAsync(
                        transaction.Branch,
                        transaction.Lines.OfType<RetailLineItem>().Select(line => line.Sku),
                        cancellationToken).ConfigureAwait(false)
                    : [];
                reverse(transaction, manager, command.Reason, stock, clock.UtcNow);
                await access.SaveAsync(transaction, cancellationToken).ConfigureAwait(false);
                await ledger.SaveAsync(stock, cancellationToken).ConfigureAwait(false);
                events.Publish(transaction.TakeEvents());
            },
            cancellationToken);
    }
}
