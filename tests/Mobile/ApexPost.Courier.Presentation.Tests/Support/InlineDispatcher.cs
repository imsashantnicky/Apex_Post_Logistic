using ApexPost.Courier.Presentation.Ports;

namespace ApexPost.Courier.Presentation.Tests.Support;

/// <summary>Runs every posted action at once, as if the test thread were the UI thread.</summary>
public sealed class InlineDispatcher : IUiDispatcher
{
    public int Posted { get; private set; }

    public void Post(Action action)
    {
        Posted++;
        action();
    }
}
