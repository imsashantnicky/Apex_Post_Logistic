using ApexPost.Application.Tracking;

namespace ApexPost.Tracking.UnitTests.Support;

/// <summary>The courier of the request in service tests; by default the courier of the test scans.</summary>
internal sealed class FixedCourier(string? courierId = TestData.CourierId) : ICurrentCourier
{
    public string? CourierId { get; } = courierId;
}
