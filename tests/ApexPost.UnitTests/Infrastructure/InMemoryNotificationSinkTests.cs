using ApexPost.Application.DTOs;
using ApexPost.Domain.Branches;
using ApexPost.Infrastructure.Notifications;
using ApexPost.UnitTests.Support;
using static ApexPost.UnitTests.Support.CounterSteps;

namespace ApexPost.UnitTests.Infrastructure;

public sealed class InMemoryNotificationSinkTests
{
    private static readonly BranchCode Home = new(TestData.HomeBranch);
    private static readonly BranchCode Other = new(TestData.OtherBranch);

    [Fact]
    public async Task Recent_returns_the_newest_of_one_branch_first()
    {
        var sink = new InMemoryNotificationSink();
        sink.Publish(Item(Home, "first"));
        sink.Publish(Item(Other, "other"));
        sink.Publish(Item(Home, "second"));

        var recent = await sink.RecentAsync(Home, 10, Ct);

        Assert.Equal(["second", "first"], recent.Select(item => item.Message));
    }

    [Fact]
    public async Task No_branch_means_all_branches()
    {
        var sink = new InMemoryNotificationSink();
        sink.Publish(Item(Home, "first"));
        sink.Publish(Item(Other, "other"));

        Assert.Equal(2, (await sink.RecentAsync(null, 10, Ct)).Count);
    }

    [Fact]
    public async Task Oldest_entries_leave_when_the_capacity_is_reached()
    {
        var sink = new InMemoryNotificationSink();
        for (var number = 0; number <= InMemoryNotificationSink.Capacity; number++)
        {
            sink.Publish(Item(Home, $"message {number}"));
        }

        var recent = await sink.RecentAsync(null, InMemoryNotificationSink.Capacity * 2, Ct);

        Assert.Equal(InMemoryNotificationSink.Capacity, recent.Count);
        Assert.DoesNotContain(recent, item => item.Message == "message 0");
    }

    private static Notification Item(BranchCode branch, string message) =>
        new(NotificationKind.LowStock, branch, message, TestData.Now);
}
