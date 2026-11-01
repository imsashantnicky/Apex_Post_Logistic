using ApexPost.Application.DTOs;
using ApexPost.Domain.Branches;

namespace ApexPost.Application.Abstractions;

/// <summary>
/// Receives simulated notifications. Publish is synchronous because it is called from C# event handlers.
/// </summary>
public interface INotificationSink
{
    void Publish(Notification notification);

    /// <summary>The newest notifications first; a null branch means all branches.</summary>
    Task<IReadOnlyList<Notification>> RecentAsync(BranchCode? branch, int count, CancellationToken cancellationToken);
}
