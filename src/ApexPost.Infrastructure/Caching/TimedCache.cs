using ApexPost.Application.Abstractions;

namespace ApexPost.Infrastructure.Caching;

/// <summary>
/// Holds one list in memory for a fixed time. A hit completes synchronously, which is why the callers return
/// ValueTask; only an expired or empty cache loads, and one SemaphoreSlim makes parallel misses load once.
/// </summary>
public sealed class TimedCache<T>(
    Func<CancellationToken, Task<IReadOnlyList<T>>> load,
    IClock clock,
    TimeSpan timeToLive) : IDisposable
{
    private readonly SemaphoreSlim _loadLock = new(1, 1);
    private volatile Snapshot? _snapshot;
    private int _hits;
    private int _loads;

    public int Hits => Volatile.Read(ref _hits);

    public int Loads => Volatile.Read(ref _loads);

    public ValueTask<IReadOnlyList<T>> GetAsync(CancellationToken cancellationToken)
    {
        if (Fresh() is { } items)
        {
            Interlocked.Increment(ref _hits);
            return ValueTask.FromResult(items);
        }

        return new ValueTask<IReadOnlyList<T>>(LoadAsync(cancellationToken));
    }

    public void Dispose() => _loadLock.Dispose();

    private async Task<IReadOnlyList<T>> LoadAsync(CancellationToken cancellationToken)
    {
        await _loadLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (Fresh() is { } items)
            {
                Interlocked.Increment(ref _hits);
                return items;
            }

            var loaded = await load(cancellationToken).ConfigureAwait(false);
            _snapshot = new Snapshot(loaded, clock.UtcNow + timeToLive);
            Interlocked.Increment(ref _loads);
            return loaded;
        }
        finally
        {
            _loadLock.Release();
        }
    }

    private IReadOnlyList<T>? Fresh() => _snapshot is { } snapshot && clock.UtcNow < snapshot.ExpiresAt
        ? snapshot.Items
        : null;

    private sealed record Snapshot(IReadOnlyList<T> Items, DateTimeOffset ExpiresAt);
}
