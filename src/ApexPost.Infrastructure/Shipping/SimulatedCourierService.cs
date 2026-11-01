using ApexPost.Application.Abstractions;
using ApexPost.Application.DTOs;
using ApexPost.Application.Exceptions;
using ApexPost.Application.Settings;
using Microsoft.Extensions.Options;

namespace ApexPost.Infrastructure.Shipping;

/// <summary>
/// Stands in for the courier's tracking API. At most <see cref="CourierSettings.MaxConcurrentRequests"/> calls
/// run at once (SemaphoreSlim); waiting for a slot counts toward the timeout. Three outcomes stay apart:
/// the caller cancelled (OperationCanceledException), the courier was too slow
/// (<see cref="ExternalServiceTimeoutException"/>) or the courier failed (<see cref="ExternalServiceException"/>).
/// Numbers starting with SLOW never answer, FAIL fails, anything else (for example APX123456) is in transit.
/// </summary>
public sealed class SimulatedCourierService : ICourierTrackingService, IDisposable
{
    public const string SlowPrefix = "SLOW";
    public const string FailPrefix = "FAIL";
    public const string InTransit = "In transit";
    public const string SortingHub = "Central sorting hub";

    private static readonly TimeSpan SimulatedLatency = TimeSpan.FromMilliseconds(20);

    private readonly IClock _clock;
    private readonly TimeSpan _timeout;
    private readonly SemaphoreSlim _slots;

    public SimulatedCourierService(IClock clock, IOptions<CourierSettings> settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _clock = clock;
        _timeout = TimeSpan.FromMilliseconds(settings.Value.TimeoutMilliseconds);
        _slots = new SemaphoreSlim(settings.Value.MaxConcurrentRequests, settings.Value.MaxConcurrentRequests);
    }

    public async Task<ParcelStatus> GetStatusAsync(string trackingNumber, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(trackingNumber);
        var number = trackingNumber.Trim().ToUpperInvariant();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_timeout);
        try
        {
            await _slots.WaitAsync(timeout.Token).ConfigureAwait(false);
            try
            {
                return await LookUpAsync(number, timeout.Token).ConfigureAwait(false);
            }
            finally
            {
                _slots.Release();
            }
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ExternalServiceTimeoutException(
                $"The courier did not answer within {_timeout.TotalMilliseconds} ms.",
                exception);
        }
    }

    public void Dispose() => _slots.Dispose();

    private async Task<ParcelStatus> LookUpAsync(string number, CancellationToken cancellationToken)
    {
        if (number.StartsWith(SlowPrefix, StringComparison.Ordinal))
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
        }

        await Task.Delay(SimulatedLatency, cancellationToken).ConfigureAwait(false);
        if (number.StartsWith(FailPrefix, StringComparison.Ordinal))
        {
            throw new ExternalServiceException("The courier tracking service is not available.");
        }

        return new ParcelStatus(number, InTransit, SortingHub, _clock.UtcNow);
    }
}
