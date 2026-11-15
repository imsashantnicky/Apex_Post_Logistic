using ApexPost.Application.Tracking;
using ApexPost.Domain.Tracking;

namespace ApexPost.Api.Seeding;

/// <summary>
/// Ten fictional shipments, APX-20000001 to APX-20000010, in different cities and statuses, for Development only.
/// They go through <see cref="IShipmentTrackingService"/>, so the seed obeys the same rules as the API. A shipment
/// that already exists is left as it is, so the seed can run on every start.
/// </summary>
public sealed partial class DevelopmentShipmentSeeder(
    IShipmentTrackingService tracking,
    ITrackingQueries queries,
    ILogger<DevelopmentShipmentSeeder> logger)
{
    private static readonly SeedShipment[] Seeds =
    [
        new("APX-20000001", "Ava Example", "+44 20 7946 0101", "12 Quay Street", "Harbour Point", 1.2m,
            ShipmentStatus.Created),
        new("APX-20000002", "Ben Sample", "+44 20 7946 0102", "4 Mill Lane", "Northgate", 0.5m,
            ShipmentStatus.Created),
        new("APX-20000003", "Cara Demo", "+44 20 7946 0103", "88 Station Road", "Westfield", 3.75m,
            ShipmentStatus.PickedUp),
        new("APX-20000004", "Dan Fiction", "+44 20 7946 0104", "2 Ferry Walk", "Old Quay", 12m,
            ShipmentStatus.PickedUp),
        new("APX-20000005", "Eve Placeholder", "+44 20 7946 0105", "17 Runway Close", "Southmere", 7.25m,
            ShipmentStatus.InTransit),
        new("APX-20000006", "Finn Testcase", "+44 20 7946 0106", "9 River Row", "Riverton", 2m,
            ShipmentStatus.InTransit),
        new("APX-20000007", "Gia Mockup", "+44 20 7946 0107", "31 Lake View", "Lakeside", 0.8m,
            ShipmentStatus.OutForDelivery),
        new("APX-20000008", "Hal Specimen", "+44 20 7946 0108", "6 Hill Crescent", "Hillcrest", 25.5m,
            ShipmentStatus.Delivered),
        new("APX-20000009", "Ida Pretend", "+44 20 7946 0109", "44 Brook Street", "Eastbrook", 4.4m,
            ShipmentStatus.Delivered),
        new("APX-20000010", "Jon Madeup", "+44 20 7946 0110", "1 Market Square", "Kingsbridge", 10m,
            ShipmentStatus.Failed),
    ];

    /// <summary>The tracking numbers of the demo shipments.</summary>
    public static IReadOnlyList<string> TrackingNumbers { get; } = [.. Seeds.Select(seed => seed.TrackingNumber)];

    /// <summary>Adds the demo shipments that do not exist yet and returns how many were added.</summary>
    public async Task<int> SeedAsync(CancellationToken cancellationToken)
    {
        var added = 0;
        foreach (var seed in Seeds)
        {
            if (await queries.FindAsync(seed.TrackingNumber, cancellationToken).ConfigureAwait(false) is not null)
            {
                continue;
            }

            await tracking.CreateAsync(seed.TrackingNumber, seed.Command, cancellationToken).ConfigureAwait(false);
            if (seed.Status != ShipmentStatus.Created)
            {
                await tracking.ChangeStatusAsync(seed.TrackingNumber, seed.Status, cancellationToken)
                    .ConfigureAwait(false);
            }

            added++;
        }

        LogSeeded(logger, added, Seeds.Length);
        return added;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Development seed added {Added} of {Total} shipments.")]
    private static partial void LogSeeded(ILogger logger, int added, int total);

    private sealed record SeedShipment(
        string TrackingNumber,
        string Name,
        string Phone,
        string Address,
        string City,
        decimal WeightKg,
        ShipmentStatus Status)
    {
        public ShipmentCommand Command => new(Name, Phone, Address, City, WeightKg);
    }
}
