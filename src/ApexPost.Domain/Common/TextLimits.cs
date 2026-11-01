namespace ApexPost.Domain.Common;

/// <summary>Maximum text lengths shared by the domain and the input validators.</summary>
public static class TextLimits
{
    public const int Name = 100;
    public const int AddressLine = 200;
    public const int PostalCode = 12;
    public const int Email = 254;
    public const int Reason = 500;
}
