using ApexPost.Application.Abstractions;
using ApexPost.Application.DTOs;
using ApexPost.Domain.Branches;

namespace ApexPost.Infrastructure.Notifications;

/// <summary>
/// Simulated notification channel: keeps the newest notifications in memory for the Notifications page.
/// Thread-safe, because events are raised from parallel requests. Register as a singleton.
/// </summary>
public sealed class InMemoryNotificationSink : INotificationSink
{
    public const int Capacity = 200;

    private readonly Queue<Notification> _items = new();
    private readonly Lock _lock = new();

    public void Publish(Notification notification)
    {
        ArgumentNullException.ThrowIfNull(notification);
        lock (_lock)
        {
            _items.Enqueue(notification);
            if (_items.Count > Capacity)
            {
                _items.Dequeue();
            }
        }
    }

    public Task<IReadOnlyList<Notification>> RecentAsync(
        BranchCode? branch,
        int count,
        CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            return Task.FromResult<IReadOnlyList<Notification>>(
            [
                .. _items.Reverse().Where(item => branch is null || item.Branch == branch).Take(count),
            ]);
        }
    }
}
