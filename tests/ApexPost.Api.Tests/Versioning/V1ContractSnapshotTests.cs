using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using ApexPost.Api.Tests.Support;
using ApexPost.Domain.Tracking;
using static ApexPost.Api.Tests.Support.ApiClient;

namespace ApexPost.Api.Tests.Versioning;

/// <summary>
/// The v1 JSON is the Part B contract and must not change: flat recipient fields and numeric enums, the same on
/// /api/v1/shipments and on the /api/shipments alias. The snapshots are the exact bodies of the seeded shipment.
/// </summary>
public sealed partial class V1ContractSnapshotTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string Seeded = "APX-20000001";
    private const string CreatedAtPlaceholder = "\"createdAtUtc\":\"<time>\"";

    // The Part B body of the seeded shipment APX-20000001, with only createdAtUtc replaced.
    private const string ShipmentSnapshot =
        "{\"trackingNumber\":\"APX-20000001\",\"recipientName\":\"Ava Example\"," +
        "\"recipientPhone\":\"+44 20 7946 0101\",\"recipientAddress\":\"12 Quay Street\"," +
        "\"recipientCity\":\"Harbour Point\",\"weightKg\":1.200,\"status\":1," +
        CreatedAtPlaceholder + ",\"version\":1}";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static TheoryData<string> V1Routes => [Shipments, V1Shipments];

    [Theory]
    [MemberData(nameof(V1Routes))]
    public async Task Shipment_json_is_exactly_the_part_B_snapshot(string route)
    {
        using var client = factory.CreateClient();

        var json = await client.GetStringAsync($"{route}/{Seeded}", Ct);

        Assert.Equal(ShipmentSnapshot, CreatedAt().Replace(json, CreatedAtPlaceholder));
    }

    [Theory]
    [MemberData(nameof(V1Routes))]
    public async Task Page_json_keeps_its_property_names_and_kinds(string route)
    {
        using var client = factory.CreateClient();

        using var page = JsonDocument.Parse(await client.GetStringAsync($"{route}?pageSize=1", Ct));

        Assert.Equal(
            ["items:Array", "page:Number", "pageSize:Number", "totalCount:Number", "totalPages:Number",
                "hasNext:True"],
            Shape(page.RootElement));
        Assert.Equal(
            ["trackingNumber:String", "recipientName:String", "recipientPhone:String", "recipientAddress:String",
                "recipientCity:String", "weightKg:Number", "status:Number", "createdAtUtc:String", "version:Number"],
            Shape(page.RootElement.GetProperty("items")[0]));
    }

    [Theory]
    [MemberData(nameof(V1Routes))]
    public async Task Scan_json_keeps_the_numeric_scan_type(string route)
    {
        using var client = factory.CreateClient();
        var trackingNumber = await CreateShipmentAsync(client, Ct);
        var upload = new { scans = new[] { Scan(trackingNumber, ScanType.Pickup) } };
        var posted = await client.PostAsJsonAsync($"{route}/{trackingNumber}/scans", upload, Ct);
        Assert.Equal(HttpStatusCode.OK, posted.StatusCode);

        using var scans = JsonDocument.Parse(await client.GetStringAsync($"{route}/{trackingNumber}/scans", Ct));

        var scan = Assert.Single(scans.RootElement.EnumerateArray());
        Assert.Equal(
            ["scanId:String", "trackingNumber:String", "scanType:Number", "courierId:String", "location:String",
                "scannedAtUtc:String", "notes:Null", "receivedAtUtc:String"],
            Shape(scan));
        Assert.Equal((int)ScanType.Pickup, scan.GetProperty("scanType").GetInt32());
    }

    [Fact]
    public async Task Versioned_v1_create_points_its_Location_to_the_versioned_route()
    {
        using var client = factory.CreateClient();
        var template = Requests.Create();
        var trackingNumber = NewTrackingNumber();
        var request = new
        {
            trackingNumber,
            template.RecipientName,
            template.RecipientPhone,
            template.RecipientAddress,
            template.RecipientCity,
            template.WeightKg,
        };

        var response = await client.PostAsJsonAsync(V1Shipments, request, Ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal($"{V1Shipments}/{trackingNumber}", response.Headers.Location?.AbsolutePath);
    }

    /// <summary>"name:ValueKind" of every property, in the order the API writes them.</summary>
    public static string[] Shape(JsonElement element) =>
        [.. element.EnumerateObject().Select(property => $"{property.Name}:{property.Value.ValueKind}")];

    [GeneratedRegex("\"createdAtUtc\":\"[^\"]+\"")]
    private static partial Regex CreatedAt();
}
