using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ApexPost.Api.Tests.Support;
using static ApexPost.Api.Tests.Support.ApiClient;
using static ApexPost.Api.Tests.Versioning.V1ContractSnapshotTests;

namespace ApexPost.Api.Tests.Versioning;

/// <summary>
/// The v2 contract: nested recipient, latestScan (null without scans) and enums as strings, on the same service as v1.
/// </summary>
public sealed class V2ContractTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly string[] ShipmentShape =
    [
        "trackingNumber:String", "recipient:Object", "weightKg:Number", "status:String", "latestScan:Null",
        "createdAtUtc:String", "version:Number",
    ];

    private static readonly string[] RecipientShape = ["name:String", "phone:String", "address:String", "city:String"];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Create_answers_201_with_the_v2_shape_and_a_v2_Location()
    {
        using var client = factory.CreateClient();
        var trackingNumber = NewTrackingNumber();

        var response = await client.PostAsJsonAsync(V2Shipments, NewShipment(trackingNumber), Ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal($"{V2Shipments}/{trackingNumber}", response.Headers.Location?.AbsolutePath);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        Assert.Equal(ShipmentShape, Shape(body.RootElement));
        Assert.Equal(RecipientShape, Shape(body.RootElement.GetProperty("recipient")));
        Assert.Equal("Created", body.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Get_shows_the_latest_scan_and_status_by_name_after_a_v2_upload()
    {
        using var client = factory.CreateClient();
        var trackingNumber = await CreateV2Async(client);
        await UploadV2Async(client, trackingNumber, "Pickup", 30);
        await UploadV2Async(client, trackingNumber, "InTransit", 10);

        using var shipment = JsonDocument.Parse(await client.GetStringAsync($"{V2Shipments}/{trackingNumber}", Ct));

        Assert.Equal("InTransit", shipment.RootElement.GetProperty("status").GetString());
        var latest = shipment.RootElement.GetProperty("latestScan");
        Assert.Equal(["scanType:String", "location:String", "scannedAtUtc:String"], Shape(latest));
        Assert.Equal("InTransit", latest.GetProperty("scanType").GetString());
        Assert.Equal("Central Hub", latest.GetProperty("location").GetString());
    }

    [Fact]
    public async Task Scans_and_list_use_the_v2_shape()
    {
        using var client = factory.CreateClient();
        var trackingNumber = await CreateV2Async(client);
        await UploadV2Async(client, trackingNumber, "Pickup", 30);

        using var scans = JsonDocument.Parse(await client.GetStringAsync($"{V2Shipments}/{trackingNumber}/scans", Ct));
        using var page = JsonDocument.Parse(await client.GetStringAsync($"{V2Shipments}?status=PickedUp", Ct));

        Assert.Equal("Pickup", Assert.Single(scans.RootElement.EnumerateArray()).GetProperty("scanType").GetString());
        var items = page.RootElement.GetProperty("items").EnumerateArray().ToList();
        Assert.NotEmpty(items);
        Assert.All(items, item =>
        {
            Assert.Equal("PickedUp", item.GetProperty("status").GetString());
            Assert.Equal(RecipientShape, Shape(item.GetProperty("recipient")));
        });
    }

    [Fact]
    public async Task Status_is_changed_by_name_and_an_unknown_name_is_400()
    {
        using var client = factory.CreateClient();
        var trackingNumber = await CreateV2Async(client);
        var route = $"{V2Shipments}/{trackingNumber}/status";

        var moved = await client.PatchAsJsonAsync(route, new { status = "PickedUp" }, Ct);
        var unknown = await client.PatchAsJsonAsync(route, new { status = "Teleported" }, Ct);

        Assert.Equal(HttpStatusCode.OK, moved.StatusCode);
        using var body = JsonDocument.Parse(await moved.Content.ReadAsStringAsync(Ct));
        Assert.Equal("PickedUp", body.RootElement.GetProperty("status").GetString());
        await Problem.ReadAsync(unknown, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Missing_or_invalid_nested_recipient_is_400_validation_problem()
    {
        using var client = factory.CreateClient();
        var noRecipient = new { trackingNumber = NewTrackingNumber(), weightKg = 1.0m };
        var badPhone = new
        {
            trackingNumber = NewTrackingNumber(),
            recipient = new { name = "Kim Example", phone = "call me", address = "5 Dock Road", city = "Westfield" },
            weightKg = 1.0m,
        };

        var missing = await Problem.ReadAsync(
            await client.PostAsJsonAsync(V2Shipments, noRecipient, Ct),
            HttpStatusCode.BadRequest);
        var invalid = await Problem.ReadAsync(
            await client.PostAsJsonAsync(V2Shipments, badPhone, Ct),
            HttpStatusCode.BadRequest);

        Assert.Contains(ErrorKeys(missing), key => key.Equals("Recipient", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(ErrorKeys(invalid), key => key.EndsWith("Phone", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Both_versions_read_the_same_shipment_from_the_same_service()
    {
        using var client = factory.CreateClient();
        var trackingNumber = await CreateV2Async(client);

        using var v1 = JsonDocument.Parse(await client.GetStringAsync($"{Shipments}/{trackingNumber}", Ct));
        using var v2 = JsonDocument.Parse(await client.GetStringAsync($"{V2Shipments}/{trackingNumber}", Ct));

        var flat = v1.RootElement;
        var recipient = v2.RootElement.GetProperty("recipient");
        Assert.Equal(flat.GetProperty("recipientName").GetString(), recipient.GetProperty("name").GetString());
        Assert.Equal(flat.GetProperty("recipientCity").GetString(), recipient.GetProperty("city").GetString());
        Assert.Equal(1, flat.GetProperty("status").GetInt32());
        Assert.Equal("Created", v2.RootElement.GetProperty("status").GetString());
        Assert.Equal(flat.GetProperty("version").GetInt32(), v2.RootElement.GetProperty("version").GetInt32());
    }

    private static object NewShipment(string trackingNumber) => new
    {
        trackingNumber,
        recipient = new
        {
            name = "Kim Example",
            phone = "+44 20 7946 0111",
            address = "5 Dock Road",
            city = "Harbour Point",
        },
        weightKg = 2.5m,
    };

    private static async Task<string> CreateV2Async(HttpClient client)
    {
        var trackingNumber = NewTrackingNumber();
        var response = await client.PostAsJsonAsync(V2Shipments, NewShipment(trackingNumber), Ct);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return trackingNumber;
    }

    private static async Task UploadV2Async(HttpClient client, string trackingNumber, string scanType, int minutesAgo)
    {
        var scan = new
        {
            scanId = NewScanId(),
            trackingNumber,
            scanType,
            courierId = "CR-101",
            location = "Central Hub",
            scannedAtUtc = DateTime.UtcNow.AddMinutes(-minutesAgo),
        };
        var route = $"{V2Shipments}/{trackingNumber}/scans";
        var response = await client.PostAsJsonAsync(route, new { scans = new[] { scan } }, Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var result = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        Assert.Equal(1, result.RootElement.GetProperty("accepted").GetInt32());
    }

    private static IEnumerable<string> ErrorKeys(Problem problem) =>
        problem.Root.GetProperty("errors").EnumerateObject().Select(error => error.Name);
}
