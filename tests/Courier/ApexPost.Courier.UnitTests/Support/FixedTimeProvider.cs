namespace ApexPost.Courier.UnitTests.Support;

/// <summary>A clock that always returns the same instant, so logged timestamps can be asserted.</summary>
internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}
