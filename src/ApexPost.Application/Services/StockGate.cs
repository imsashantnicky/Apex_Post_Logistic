using System.Collections.Concurrent;
using ApexPost.Domain.Branches;

namespace ApexPost.Application.Services;

/// <summary>
/// Lets one stock change per branch run at a time (one SemaphoreSlim per branch), so two counters cannot sell
/// the last item twice. Branches do not wait for each other. Register as a singleton.
/// </summary>
public sealed class StockGate : IDisposable
{
    private readonly ConcurrentDictionary<BranchCode, SemaphoreSlim> _gates = new();

    public async Task<T> RunAsync<T>(BranchCode branch, Func<Task<T>> work, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(work);
        var gate = _gates.GetOrAdd(branch, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await work().ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    public Task RunAsync(BranchCode branch, Func<Task> work, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(work);
        return RunAsync(
            branch,
            async () =>
            {
                await work().ConfigureAwait(false);
                return true;
            },
            cancellationToken);
    }

    public void Dispose()
    {
        foreach (var gate in _gates.Values)
        {
            gate.Dispose();
        }

        _gates.Clear();
    }
}
