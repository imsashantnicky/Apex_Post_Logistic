using System.Globalization;
using ApexPost.Application.Abstractions;
using ApexPost.Application.DTOs;
using ApexPost.Application.Settings;
using ApexPost.Domain.Common;
using ApexPost.Domain.Events;
using Microsoft.Extensions.Options;

namespace ApexPost.Application.Services;

/// <summary>
/// Turns domain events into simulated notifications: low stock, a money order above the high-value amount and
/// every void or refund. Subscribes on construction and unsubscribes on dispose. Register as a singleton.
/// </summary>
public sealed class NotificationSubscriber : IDisposable
{
    private readonly CounterEvents _events;
    private readonly INotificationSink _sink;
    private readonly Money _highValueAmount;

    public NotificationSubscriber(CounterEvents events, INotificationSink sink, IOptions<MoneyOrderSettings> settings)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(settings);
        _events = events;
        _sink = sink;
        _highValueAmount = new Money(settings.Value.HighValueAlertAmount);
        _events.StockLow += OnStockLow;
        _events.MoneyOrderIssued += OnMoneyOrderIssued;
        _events.TransactionVoided += OnTransactionVoided;
        _events.TransactionRefunded += OnTransactionRefunded;
    }

    public void Dispose()
    {
        _events.StockLow -= OnStockLow;
        _events.MoneyOrderIssued -= OnMoneyOrderIssued;
        _events.TransactionVoided -= OnTransactionVoided;
        _events.TransactionRefunded -= OnTransactionRefunded;
    }

    private void OnStockLow(object? sender, StockLow low) =>
        _sink.Publish(new Notification(
            NotificationKind.LowStock,
            low.Branch,
            string.Create(CultureInfo.InvariantCulture, $"Low stock: {low.Sku} has {low.Quantity} left."),
            low.OccurredAt));

    private void OnMoneyOrderIssued(object? sender, MoneyOrderIssued issued)
    {
        if (issued.Amount > _highValueAmount)
        {
            _sink.Publish(new Notification(
                NotificationKind.HighValueMoneyOrder,
                issued.Branch,
                $"High-value money order of {issued.Amount} in transaction {issued.TransactionId}.",
                issued.OccurredAt));
        }
    }

    private void OnTransactionVoided(object? sender, TransactionVoided voided) =>
        _sink.Publish(new Notification(
            NotificationKind.Void,
            voided.Branch,
            $"Transaction {voided.TransactionId} ({voided.Total}) was voided: {voided.Reason}",
            voided.OccurredAt));

    private void OnTransactionRefunded(object? sender, TransactionRefunded refunded) =>
        _sink.Publish(new Notification(
            NotificationKind.Refund,
            refunded.Branch,
            $"Transaction {refunded.TransactionId} ({refunded.Total}) was refunded: {refunded.Reason}",
            refunded.OccurredAt));
}
