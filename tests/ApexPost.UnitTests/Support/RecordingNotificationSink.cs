using ApexPost.Application.Abstractions;
using ApexPost.Application.DTOs;
using ApexPost.Domain.Branches;

namespace ApexPost.UnitTests.Support;

internal sealed class RecordingNotificationSink : INotificationSink
{
    private readonly List<Notification> _notifications = [];

    public IReadOnlyList<Notification> Published => _notifications;

    public void Publish(Notification notification) => _notifications.Add(notification);

    public Task<IReadOnlyList<Notification>> RecentAsync(
        BranchCode? branch,
        int count,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Notification>>(
        [
            .. _notifications.Where(item => branch is null || item.Branch == branch).Reverse().Take(count),
        ]);
}
