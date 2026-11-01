using ApexPost.Domain.Common;
using ApexPost.Domain.Shipping;

namespace ApexPost.Infrastructure.Seeding;

/// <summary>Ten destination zones from Z01 Local to Z10 Rest of world, dearer the further away.</summary>
public static class DemoZones
{
    private static readonly (string Code, string Name, decimal BaseRate, decimal PerKgRate)[] Rows =
    [
        ("Z01", "Local", 3.50m, 0.80m),
        ("Z02", "Regional", 4.50m, 1.10m),
        ("Z03", "National", 5.50m, 1.40m),
        ("Z04", "Islands", 7.00m, 1.90m),
        ("Z05", "Neighbouring countries", 9.50m, 2.60m),
        ("Z06", "Europe", 11.00m, 3.20m),
        ("Z07", "North America", 14.00m, 4.10m),
        ("Z08", "Asia Pacific", 15.50m, 4.60m),
        ("Z09", "Middle East and Africa", 16.00m, 4.90m),
        ("Z10", "Rest of world", 18.00m, 5.50m),
    ];

    public static IReadOnlyList<ShippingZone> Create() =>
    [
        .. Rows.Select(row => new ShippingZone(row.Code, row.Name, new Money(row.BaseRate), new Money(row.PerKgRate))),
    ];
}
