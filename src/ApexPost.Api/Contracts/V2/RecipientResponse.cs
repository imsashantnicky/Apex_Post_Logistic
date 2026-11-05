namespace ApexPost.Api.Contracts.V2;

/// <summary>The recipient of a shipment in a v2 answer.</summary>
/// <param name="Name">Who receives the shipment.</param>
/// <param name="Phone">The phone number.</param>
/// <param name="Address">The street address.</param>
/// <param name="City">The city.</param>
public sealed record RecipientResponse(string Name, string Phone, string Address, string City)
{
    /// <summary>Who receives the shipment.</summary>
    /// <example>Ava Example</example>
    public string Name { get; init; } = Name;

    /// <summary>The phone number.</summary>
    /// <example>+44 20 7946 0101</example>
    public string Phone { get; init; } = Phone;

    /// <summary>The street address.</summary>
    /// <example>12 Quay Street</example>
    public string Address { get; init; } = Address;

    /// <summary>The city.</summary>
    /// <example>Harbour Point</example>
    public string City { get; init; } = City;
}
