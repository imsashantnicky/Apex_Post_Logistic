using ApexPost.Application.Abstractions;

namespace ApexPost.UnitTests.Support;

internal sealed class FixedClock : IClock
{
    public DateTimeOffset UtcNow { get; set; } = TestData.Now;
}
