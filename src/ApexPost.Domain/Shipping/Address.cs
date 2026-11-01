using ApexPost.Domain.Common;

namespace ApexPost.Domain.Shipping;

/// <summary>A delivery address; every part is required and trimmed.</summary>
public sealed record Address
{
    public required string Name { get; init => field = Guard.NotBlank(value, nameof(Name), TextLimits.Name); }

    public required string Street
    {
        get;
        init => field = Guard.NotBlank(value, nameof(Street), TextLimits.AddressLine);
    }

    public required string City { get; init => field = Guard.NotBlank(value, nameof(City), TextLimits.Name); }

    public required string PostalCode
    {
        get;
        init => field = Guard.NotBlank(value, nameof(PostalCode), TextLimits.PostalCode);
    }

    public required string Country { get; init => field = Guard.NotBlank(value, nameof(Country), TextLimits.Name); }
}
