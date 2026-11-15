using ApexPost.Courier.Offline.Outbox;
using ApexPost.Courier.Presentation.Ports;

namespace ApexPost.Courier.Presentation.Tests.Support;

public sealed class FakeSyncRunner : ISyncRunner
{
    public event EventHandler<OutboxResult>? SyncCompleted;

    public List<string> Triggers { get; } = [];

    public OutboxResult NextResult { get; set; } = OutboxResult.Empty;

    public bool HasSubscribers => SyncCompleted is not null;

    public void SyncInBackground(string trigger) => Triggers.Add(trigger);

    public Task<OutboxResult> SyncNowAsync(string trigger, CancellationToken ct)
    {
        Triggers.Add(trigger);
        if (NextResult != OutboxResult.Empty)
        {
            Raise(NextResult);
        }

        return Task.FromResult(NextResult);
    }

    public void Raise(OutboxResult result) => SyncCompleted?.Invoke(this, result);
}
