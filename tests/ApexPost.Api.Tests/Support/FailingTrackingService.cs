using ApexPost.Application.DTOs;
using ApexPost.Application.Tracking;
using ApexPost.Domain.Tracking;

namespace ApexPost.Api.Tests.Support;

/// <summary>Fault injection: every use case fails like a broken database or a bug, to prove the 500.</summary>
public sealed class FailingTrackingService : IShipmentTrackingService
{
    public const string SecretMessage = "Internal failure detail that must never leak.";

    public Task<PagedResult<ShipmentView>> ListAsync(
        int page,
        int pageSize,
        ShipmentStatus? status,
        string? city,
        CancellationToken cancellationToken) => throw Fault();

    public Task<ShipmentView> GetAsync(string trackingNumber, CancellationToken cancellationToken) => throw Fault();

    public Task<ShipmentView> CreateAsync(
        string trackingNumber,
        ShipmentCommand command,
        CancellationToken cancellationToken) => throw Fault();

    public Task<ShipmentView> ReplaceAsync(
        string trackingNumber,
        ShipmentCommand command,
        CancellationToken cancellationToken) => throw Fault();

    public Task<ShipmentView> ChangeStatusAsync(
        string trackingNumber,
        ShipmentStatus status,
        CancellationToken cancellationToken) => throw Fault();

    public Task DeleteAsync(string trackingNumber, CancellationToken cancellationToken) => throw Fault();

    public Task<ScanUploadResult> UploadScansAsync(
        IReadOnlyList<ScanUpload> scans,
        CancellationToken cancellationToken) => throw Fault();

    public Task<ScanUploadResult> UploadShipmentScansAsync(
        string trackingNumber,
        IReadOnlyList<ScanUpload> scans,
        CancellationToken cancellationToken) => throw Fault();

    public Task<IReadOnlyList<ScanView>> GetScansAsync(string trackingNumber, CancellationToken cancellationToken) =>
        throw Fault();

    private static InvalidOperationException Fault() => new(SecretMessage);
}
