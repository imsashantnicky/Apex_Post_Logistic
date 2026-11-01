using ApexPost.Domain.Branches;
using ApexPost.Domain.Common;
using ApexPost.Domain.Events;
using ApexPost.Domain.Inventory;
using ApexPost.Domain.Personnel;
using ApexPost.Domain.Verification;

namespace ApexPost.Domain.Transactions;

/// <summary>
/// One customer visit at a counter: a mixed basket of retail items, shipments and money orders.
/// Status flow: Open -> Paid -> Refunded, and Open or Paid -> Voided.
/// </summary>
public sealed class CounterTransaction : Entity<Guid>
{
    private readonly List<TransactionLine> _lines = [];
    private readonly List<IdentityVerification> _verifications = [];
    private readonly List<IDomainEvent> _events = [];

    private CounterTransaction(Guid id, BranchCode branch, Guid counterId, Guid clerkId, DateTimeOffset openedAt)
        : base(Guard.NotEmpty(id, "Transaction id"))
    {
        Branch = Guard.Required(branch, nameof(Branch));
        CounterId = Guard.NotEmpty(counterId, "Counter id");
        ClerkId = Guard.NotEmpty(clerkId, "Clerk id");
        OpenedAt = openedAt;
    }

    public BranchCode Branch { get; }

    public Guid CounterId { get; }

    public Guid ClerkId { get; }

    public DateTimeOffset OpenedAt { get; }

    public TransactionStatus Status { get; private set; } = TransactionStatus.Open;

    public IReadOnlyList<ILineItem> Lines => _lines.AsReadOnly();

    public IReadOnlyList<IdentityVerification> Verifications => _verifications.AsReadOnly();

    public Payment? Payment { get; private set; }

    public Reversal? Reversal { get; private set; }

    public Money Subtotal => Sum(line => line.Total);

    /// <summary>The tax frozen at payment; zero while the basket is open (use <see cref="PreviewTax"/>).</summary>
    public Money Tax => Payment?.Tax ?? Money.Zero(Currency);

    public Money Total => Subtotal + Tax;

    /// <summary>What the customer hands over: a cash payment is rounded to the nearest 0.05.</summary>
    public Money AmountDue => Payment is { Method: PaymentMethod.Cash } ? Total.RoundedForCash : Total;

    public bool HasPassedVerification => _verifications.Exists(verification => verification.IsPassed);

    private string Currency => _lines.Count > 0 ? _lines[0].Total.Currency : Money.DefaultCurrency;

    private IEnumerable<RetailLineItem> RetailLines => _lines.OfType<RetailLineItem>();

    /// <summary>The only way to start a transaction: the branch must be open and the clerk allowed there.</summary>
    public static CounterTransaction Open(Branch branch, Counter counter, Staff clerk, DateTimeOffset now)
    {
        branch.EnsureOpen();
        if (!branch.Counters.Contains(counter))
        {
            throw new DomainException($"The counter does not belong to branch {branch.Code}.");
        }

        clerk.EnsureCanTransactAt(branch.Code);
        return new CounterTransaction(Guid.CreateVersion7(now), branch.Code, counter.Id, clerk.Id, now);
    }

    /// <summary>Rebuilds a stored transaction and rejects a state that the domain could never have produced.</summary>
    public static CounterTransaction Restore(
        Guid id,
        BranchCode branch,
        Guid counterId,
        Guid clerkId,
        DateTimeOffset openedAt,
        TransactionStatus status,
        IEnumerable<ILineItem> lines,
        IEnumerable<IdentityVerification> verifications,
        Payment? payment,
        Reversal? reversal,
        int version)
    {
        var transaction = new CounterTransaction(id, branch, counterId, clerkId, openedAt)
        {
            Status = Guard.Defined(status, nameof(status)),
            Payment = payment,
            Reversal = reversal,
            Version = version,
        };
        foreach (var line in Guard.Required(lines, nameof(lines)))
        {
            transaction.Append(line);
        }

        foreach (var verification in Guard.Required(verifications, nameof(verifications)))
        {
            transaction._verifications.Add(Guard.Required(verification, nameof(verification)));
        }

        transaction.EnsureConsistent();
        return transaction;
    }

    /// <summary>Returns the events raised since the last call and clears them.</summary>
    public IReadOnlyList<IDomainEvent> TakeEvents()
    {
        IDomainEvent[] events = [.. _events];
        _events.Clear();
        return events;
    }

    public bool RequiresVerification(Money threshold) =>
        _lines.OfType<MoneyOrderLineItem>().Any(order => order.Amount > threshold);

    /// <summary>Tax of the open basket with the given rule; once paid, the frozen tax.</summary>
    public Money PreviewTax(Func<ILineItem, Money> taxRule) => Payment?.Tax ?? Sum(taxRule);

    public void AddLine(Staff clerk, ILineItem line)
    {
        EnsureOpen();
        clerk.EnsureCanTransactAt(Branch);
        Append(line);
    }

    public void RecordVerification(Staff clerk, IdentityVerification verification)
    {
        EnsureOpen();
        clerk.EnsureCanTransactAt(Branch);
        _verifications.Add(Guard.Required(verification, nameof(verification)));
    }

    /// <summary>Checks every rule and every stock line first; if one fails, nothing changes.</summary>
    public void Pay(
        Staff clerk,
        PaymentMethod method,
        PaymentRules rules,
        IReadOnlyCollection<StockItem> stock,
        DateTimeOffset now)
    {
        EnsureOpen();
        clerk.EnsureCanTransactAt(Branch);
        if (_lines.Count == 0)
        {
            throw new DomainException("An empty basket cannot be paid.");
        }

        if (RequiresVerification(rules.VerificationThreshold) && !HasPassedVerification)
        {
            throw new DomainException(
                $"A money order above {rules.VerificationThreshold} needs a passed identity check first.");
        }

        var payment = new Payment(method, Sum(rules.TaxRule), now);
        var changedStock = StockAllocation.Take(Branch, RetailLines, stock);
        Payment = payment;
        Status = TransactionStatus.Paid;
        RaisePaidEvents(payment, changedStock, rules.IsLowStock);
    }

    /// <summary>Voids an open or paid transaction; voiding a paid one puts its retail items back in stock.</summary>
    public void Void(Staff manager, string reason, IReadOnlyCollection<StockItem> stock, DateTimeOffset now)
    {
        if (Status is not (TransactionStatus.Open or TransactionStatus.Paid))
        {
            throw new DomainException($"This transaction is {Status} and cannot be voided.");
        }

        var reversal = Reverse(manager, reason, now);
        if (Status == TransactionStatus.Paid)
        {
            StockAllocation.Restock(Branch, RetailLines, stock);
        }

        Reversal = reversal;
        Status = TransactionStatus.Voided;
        _events.Add(new TransactionVoided(Id, Branch, manager.Id, reversal.Reason, Total, now));
    }

    /// <summary>Refunds a paid transaction and puts its retail items back in stock.</summary>
    public void Refund(Staff manager, string reason, IReadOnlyCollection<StockItem> stock, DateTimeOffset now)
    {
        if (Status != TransactionStatus.Paid)
        {
            throw new DomainException($"This transaction is {Status}; only a paid one can be refunded.");
        }

        var reversal = Reverse(manager, reason, now);
        StockAllocation.Restock(Branch, RetailLines, stock);
        Reversal = reversal;
        Status = TransactionStatus.Refunded;
        _events.Add(new TransactionRefunded(Id, Branch, manager.Id, reversal.Reason, Total, now));
    }

    private void RaisePaidEvents(Payment payment, List<StockItem> changedStock, Predicate<StockItem> isLowStock)
    {
        var now = payment.PaidAt;
        _events.Add(new TransactionCompleted(Id, Branch, Total, payment.Method, now));
        _events.AddRange(_lines
            .OfType<MoneyOrderLineItem>()
            .Select(order => new MoneyOrderIssued(Id, Branch, order.Id, order.Amount, now)));
        _events.AddRange(changedStock
            .Where(item => isLowStock(item))
            .Select(item => new StockLow(item.Branch, item.Sku, item.Quantity, now)));
    }

    private Reversal Reverse(Staff manager, string reason, DateTimeOffset now)
    {
        manager.EnsureCanReverseAt(Branch);
        return new Reversal(manager.Id, reason, now);
    }

    private void Append(ILineItem line)
    {
        Guard.Required(line, nameof(line));
        if (_lines.Count > 0 && line.Total.Currency != Currency)
        {
            throw new DomainException("All lines of a basket must use the same currency.");
        }

        if (_lines.Exists(existing => existing.Id == line.Id))
        {
            throw new DomainException("This line is already in the basket.");
        }

        _lines.Add(line as TransactionLine ?? throw new DomainException("This kind of line is not supported."));
    }

    private void EnsureOpen()
    {
        if (Status != TransactionStatus.Open)
        {
            throw new DomainException($"The transaction is {Status} and can no longer change.");
        }
    }

    private void EnsureConsistent()
    {
        var statusMatches = Status switch
        {
            TransactionStatus.Open => Payment is null && Reversal is null,
            TransactionStatus.Paid => Payment is not null && Reversal is null && _lines.Count > 0,
            TransactionStatus.Voided => Reversal is not null,
            TransactionStatus.Refunded => Payment is not null && Reversal is not null && _lines.Count > 0,
            _ => false,
        };
        var timesInOrder = (Payment is null || Payment.PaidAt >= OpenedAt)
            && (Reversal is null || Reversal.ReversedAt >= (Payment?.PaidAt ?? OpenedAt));
        var currencyMatches = Payment is null || Payment.Tax.Currency == Currency;
        if (!(statusMatches && timesInOrder && currencyMatches))
        {
            throw new DomainException($"Stored transaction {Id} is not consistent with its status {Status}.");
        }
    }

    private Money Sum(Func<ILineItem, Money> amount) =>
        _lines.Aggregate(Money.Zero(Currency), (total, line) => total + amount(line));
}
