using ApexPost.Courier.Presentation.Ports;

namespace ApexPost.Courier.Presentation.Tests.Support;

public sealed class FakePendingCountSource : IPendingCountSource
{
    public event EventHandler<int>? PendingCountChanged;

    public bool HasSubscribers => PendingCountChanged is not null;

    public void Raise(int pending) => PendingCountChanged?.Invoke(this, pending);
}
