using ApexPost.Application.Abstractions;

namespace ApexPost.Infrastructure;

/// <summary>The real time, read from the injected <see cref="TimeProvider"/>.</summary>
public sealed class SystemClock(TimeProvider timeProvider) : IClock
{
    public DateTimeOffset UtcNow => timeProvider.GetUtcNow();
}
