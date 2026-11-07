using ApexPost.Courier.Presentation.Ports;

namespace ApexPost.Courier.Presentation.Tests.Support;

public sealed class FakeNavigator : IAppNavigator
{
    public List<string> Visits { get; } = [];

    public Task ShowLoginAsync()
    {
        Visits.Add("login");
        return Task.CompletedTask;
    }

    public Task ShowMainAsync()
    {
        Visits.Add("main");
        return Task.CompletedTask;
    }
}
