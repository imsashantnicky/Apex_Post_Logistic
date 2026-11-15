namespace ApexPost.Application.Tracking;

/// <summary>
/// The courier who sends the current request, as the host knows it (the API reads the courierId claim of the
/// bearer token). The tracking service uses it to accept only scans the courier recorded.
/// </summary>
public interface ICurrentCourier
{
    /// <summary>The courier id (CR- followed by 3 digits), or null when the caller is not a courier.</summary>
    string? CourierId { get; }
}
