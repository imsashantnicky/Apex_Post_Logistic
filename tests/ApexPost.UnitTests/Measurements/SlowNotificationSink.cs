using ApexPost.Application.Abstractions;
using ApexPost.Application.DTOs;
using ApexPost.Domain.Branches;

namespace ApexPost.UnitTests.Measurements;

/// <summary>Adds the same latency to the third dashboard source, the alerts.</summary>
internal sealed class SlowNotificationSink(INotificationSink inner, TimeSpan latency) : INotificationSink
{
    public void Publish(Notification notification) => inner.Publish(notification);

    public async Task<IReadOnlyList<Notification>> RecentAsync(
        BranchCode? branch,
        int count,
        CancellationToken cancellationToken)
    {
        await Task.Delay(latency, cancellationToken);
        return await inner.RecentAsync(branch, count, cancellationToken);
    }
}
