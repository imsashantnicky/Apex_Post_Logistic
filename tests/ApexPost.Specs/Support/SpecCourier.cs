using ApexPost.Application.Tracking;

namespace ApexPost.Specs.Support;

/// <summary>The courier the in-process server sees as the caller: the courier of the scenario.</summary>
public sealed class SpecCourier(string courierId) : ICurrentCourier
{
    /// <inheritdoc />
    public string? CourierId { get; } = courierId;
}
