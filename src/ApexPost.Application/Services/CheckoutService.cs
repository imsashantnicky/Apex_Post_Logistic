using System.Globalization;
using ApexPost.Application.Abstractions;
using ApexPost.Application.DTOs;
using ApexPost.Application.Exceptions;
using ApexPost.Application.Extensions;
using ApexPost.Application.Validation;
using ApexPost.Domain.Branches;
using ApexPost.Domain.Catalogue;
using ApexPost.Domain.Common;
using ApexPost.Domain.Personnel;
using ApexPost.Domain.Shipping;
using ApexPost.Domain.Transactions;
using ApexPost.Domain.Verification;

namespace ApexPost.Application.Services;

/// <summary>
/// The counter workflow: open a transaction, build one mixed basket, verify identity and take payment.
/// Payment changes the transaction and the branch stock in one method under the branch's stock gate.
/// </summary>
public sealed class CheckoutService(
    TransactionAccess access,
    IRepository<Branch, BranchCode> branches,
    IProductCatalog products,
    ShippingPricer pricer,
    IIdentityVerificationService verifier,
    StockLedger ledger,
    StockGate gate,
    CounterEvents events,
    CheckoutRules rules,
    AttributeModelValidator validator,
    ITransactionQueries queries,
    IClock clock)
{
    private const string LabelPrefix = "APX-";
    private const int LabelIdCharacters = 8;
    private const string PlainGuidFormat = "N";

    public async Task<Guid> OpenAsync(Guid counterId, CancellationToken cancellationToken)
    {
        access.User.RequireRole(StaffRole.CounterClerk, StaffRole.BranchManager);
        var clerk = await access.CurrentStaffAsync(cancellationToken).ConfigureAwait(false);
        var branch = await branches.GetAsync(access.User.Branch, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException($"Branch {access.User.Branch} does not exist.");
        var counter = branch.Counters.FirstOrDefault(candidate => candidate.Id == counterId)
            ?? throw new NotFoundException($"Branch {branch.Code} has no such counter.");

        var transaction = CounterTransaction.Open(branch, counter, clerk, clock.UtcNow);
        await access.AddAsync(transaction, cancellationToken).ConfigureAwait(false);
        return transaction.Id;
    }

    public async Task<TransactionDetails> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var transaction = await access.ReadAsync(id, cancellationToken).ConfigureAwait(false);
        return TransactionDetails.From(transaction, rules.TaxRule, rules.VerificationThreshold);
    }

    public Task<PagedResult<TransactionSummary>> ListAsync(
        TransactionFilter filter,
        PageRequest page,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filter);
        return queries.ListAsync(filter with { Branch = access.User.ScopeTo(filter.Branch) }, page, cancellationToken);
    }

    public async Task<Guid> AddRetailItemAsync(
        Guid id,
        AddRetailItemCommand command,
        CancellationToken cancellationToken)
    {
        validator.EnsureValid(command);
        var sku = new Sku(command.Sku);
        var product = await products.FindAsync(sku, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException($"Product {sku} does not exist.");
        return await AddLineAsync(id, RetailLineItem.For(product, command.Quantity), cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<Guid> AddShippingAsync(Guid id, AddShippingCommand command, CancellationToken cancellationToken)
    {
        validator.EnsureValid(command);
        var recipient = new Address
        {
            Name = command.RecipientName,
            Street = command.Street,
            City = command.City,
            PostalCode = command.PostalCode,
            Country = command.Country,
        };
        var dimensions = new Dimensions(command.LengthCm, command.WidthCm, command.HeightCm);
        var shipment = new Shipment(
            command.Type,
            command.Level,
            Weight.FromKilograms(command.WeightKg),
            dimensions,
            recipient);
        var line = await pricer.CreateLineAsync(shipment, command.ZoneCode, cancellationToken).ConfigureAwait(false);
        return await AddLineAsync(id, line, cancellationToken).ConfigureAwait(false);
    }

    public Task<Guid> AddMoneyOrderAsync(Guid id, AddMoneyOrderCommand command, CancellationToken cancellationToken)
    {
        validator.EnsureValid(command);
        var line = MoneyOrderLineItem.Issue(new Money(command.Amount), command.Sender, command.Recipient);
        return AddLineAsync(id, line, cancellationToken);
    }

    /// <summary>The service decides the result; only the masked document number is stored.</summary>
    public async Task<VerificationResult> VerifyIdentityAsync(
        Guid id,
        VerifyIdentityCommand command,
        CancellationToken cancellationToken)
    {
        validator.EnsureValid(command);
        var (transaction, clerk) = await access.ForCounterWorkAsync(id, cancellationToken).ConfigureAwait(false);
        var result = await verifier.VerifyAsync(command.DocumentType, command.DocumentNumber, cancellationToken)
            .ConfigureAwait(false);
        var verification = IdentityVerification.FromDocument(
            command.DocumentType,
            command.DocumentNumber,
            result,
            clock.UtcNow);
        transaction.RecordVerification(clerk, verification);
        await access.SaveAsync(transaction, cancellationToken).ConfigureAwait(false);
        return result;
    }

    /// <summary>
    /// Takes payment and removes the sold goods from branch stock. The transaction is saved before the stock,
    /// so a concurrent change of the same basket fails with a conflict before any stock moves.
    /// </summary>
    public Task PayAsync(Guid id, PaymentMethod method, CancellationToken cancellationToken) =>
        gate.RunAsync(
            access.User.Branch,
            async () =>
            {
                var (transaction, clerk) = await access.ForCounterWorkAsync(id, cancellationToken)
                    .ConfigureAwait(false);
                var skus = transaction.Lines.OfType<RetailLineItem>().Select(line => line.Sku);
                var stock = await ledger.LoadAsync(transaction.Branch, skus, cancellationToken).ConfigureAwait(false);
                transaction.Pay(clerk, method, rules.Payment, stock, clock.UtcNow);
                await access.SaveAsync(transaction, cancellationToken).ConfigureAwait(false);
                await ledger.SaveAsync(stock, cancellationToken).ConfigureAwait(false);
                events.Publish(transaction.TakeEvents());
            },
            cancellationToken);

    public async Task<ShippingLabel> GetLabelAsync(Guid id, Guid lineId, CancellationToken cancellationToken)
    {
        var transaction = await access.ReadAsync(id, cancellationToken).ConfigureAwait(false);
        var index = transaction.Lines.ToList().FindIndex(candidate => candidate.Id == lineId);
        if (index < 0 || transaction.Lines[index] is not ShippingLineItem line)
        {
            throw new NotFoundException($"Transaction {id} has no such shipping line.");
        }

        var branch = await branches.GetAsync(transaction.Branch, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException($"Branch {transaction.Branch} does not exist.");
        var shipment = line.Shipment;
        return new ShippingLabel(
            LabelReference(transaction.Id, index + 1),
            line.TrackingNumber,
            branch.Code,
            branch.Name,
            shipment.Recipient,
            shipment.Weight,
            shipment.Type,
            shipment.Level,
            line.ZoneCode);
    }

    private static string LabelReference(Guid id, int lineNumber)
    {
        var suffix = id.ToString(PlainGuidFormat, CultureInfo.InvariantCulture)[^LabelIdCharacters..];
        return string.Create(CultureInfo.InvariantCulture, $"{LabelPrefix}{suffix.ToUpperInvariant()}-{lineNumber}");
    }

    private async Task<Guid> AddLineAsync(Guid id, ILineItem line, CancellationToken cancellationToken)
    {
        var (transaction, clerk) = await access.ForCounterWorkAsync(id, cancellationToken).ConfigureAwait(false);
        transaction.AddLine(clerk, line);
        await access.SaveAsync(transaction, cancellationToken).ConfigureAwait(false);
        return line.Id;
    }
}
