namespace ApexPost.Api.Security;

/// <summary>The fictional couriers that may sign in through /dev/token. Development only; no real people.</summary>
public static class DevelopmentCouriers
{
    /// <summary>The first demo courier.</summary>
    public const string First = "CR-101";

    /// <summary>The second demo courier.</summary>
    public const string Second = "CR-102";

    /// <summary>Every courier id /dev/token issues a token for.</summary>
    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal) { First, Second };
}
