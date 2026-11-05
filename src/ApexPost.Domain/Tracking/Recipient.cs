using ApexPost.Domain.Common;

namespace ApexPost.Domain.Tracking;

/// <summary>Who receives a tracked shipment and where. Every part is required.</summary>
public sealed record Recipient
{
    public const int NameLength = 100;
    public const int PhoneLength = 20;
    public const int AddressLength = 200;
    public const int CityLength = 80;
    private const int MinimumPhoneDigits = 7;

    public Recipient(string name, string phone, string address, string city)
    {
        Name = Guard.NotBlank(name, "Recipient name", NameLength);
        Phone = ValidPhone(phone);
        Address = Guard.NotBlank(address, "Recipient address", AddressLength);
        City = Guard.NotBlank(city, "Recipient city", CityLength);
    }

    public string Name { get; }

    public string Phone { get; }

    public string Address { get; }

    public string City { get; }

    // An optional leading '+', then digits, spaces and dashes with at least seven digits.
    private static string ValidPhone(string phone)
    {
        var value = Guard.NotBlank(phone, "Recipient phone", PhoneLength);
        var body = value.StartsWith('+') ? value[1..] : value;
        var valid = body.All(character => char.IsAsciiDigit(character) || character is ' ' or '-')
            && body.Count(char.IsAsciiDigit) >= MinimumPhoneDigits;
        return valid
            ? value
            : throw new DomainException(
                "Recipient phone must have at least 7 digits and only digits, spaces or dashes.");
    }
}
