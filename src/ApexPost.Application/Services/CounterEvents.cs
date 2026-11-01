using ApexPost.Domain.Events;

namespace ApexPost.Application.Services;

/// <summary>
/// The observer hub: services publish the domain events of a saved aggregate, subscribers such as
/// <see cref="NotificationSubscriber"/> listen to the C# events. Register as a singleton.
/// </summary>
public sealed class CounterEvents
{
    public event EventHandler<TransactionCompleted>? TransactionCompleted;

    public event EventHandler<StockLow>? StockLow;

    public event EventHandler<MoneyOrderIssued>? MoneyOrderIssued;

    public event EventHandler<TransactionVoided>? TransactionVoided;

    public event EventHandler<TransactionRefunded>? TransactionRefunded;

    public void Publish(IEnumerable<IDomainEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        foreach (var domainEvent in events)
        {
            Raise(domainEvent);
        }
    }

    private void Raise(IDomainEvent domainEvent)
    {
        switch (domainEvent)
        {
            case TransactionCompleted completed:
                TransactionCompleted?.Invoke(this, completed);
                break;
            case StockLow low:
                StockLow?.Invoke(this, low);
                break;
            case MoneyOrderIssued issued:
                MoneyOrderIssued?.Invoke(this, issued);
                break;
            case TransactionVoided voided:
                TransactionVoided?.Invoke(this, voided);
                break;
            case TransactionRefunded refunded:
                TransactionRefunded?.Invoke(this, refunded);
                break;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(domainEvent),
                    domainEvent.GetType().Name,
                    "Unknown domain event.");
        }
    }
}
