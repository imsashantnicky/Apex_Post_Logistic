namespace ApexPost.IntegrationTests.Support;

/// <summary>Holds everyone back until all parties have arrived, so that two requests really overlap.</summary>
internal sealed class Rendezvous(int parties)
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(15);

    private readonly TaskCompletionSource _everyoneHere = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _arrived;

    public Task ArriveAsync()
    {
        if (Interlocked.Increment(ref _arrived) == parties)
        {
            _everyoneHere.TrySetResult();
        }

        return _everyoneHere.Task.WaitAsync(Patience);
    }
}
