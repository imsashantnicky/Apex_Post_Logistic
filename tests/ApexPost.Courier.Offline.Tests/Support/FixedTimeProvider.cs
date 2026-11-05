namespace ApexPost.Courier.Offline.Tests.Support;

/// <summary>A clock that always returns the same instant.</summary>
internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}
