using ApexPost.Application.DTOs;

namespace ApexPost.Application.Abstractions;

/// <summary>
/// Looks up a parcel at the courier. Throws <see cref="Exceptions.ExternalServiceTimeoutException"/> when the
/// courier is too slow, <see cref="Exceptions.ExternalServiceException"/> when it fails, and
/// <see cref="OperationCanceledException"/> only when the caller cancelled.
/// </summary>
public interface ICourierTrackingService
{
    Task<ParcelStatus> GetStatusAsync(string trackingNumber, CancellationToken cancellationToken);
}
