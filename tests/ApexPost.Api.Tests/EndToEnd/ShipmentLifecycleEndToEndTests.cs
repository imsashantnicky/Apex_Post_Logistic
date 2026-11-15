using System.Net;
using System.Net.Http.Json;
using ApexPost.Api.Contracts.V2;
using ApexPost.Api.Security;
using ApexPost.Api.Tests.Support;
using ApexPost.Domain.Tracking;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ScanUploadResult = ApexPost.Api.Contracts.V1.ScanUploadResult;

namespace ApexPost.Api.Tests.EndToEnd;

/// <summary>
/// The whole v2 shipment flow through the real API host, real services, EF Core and a throw-away SQL Server
/// database, with only authentication swapped for <see cref="TestAuthHandler"/> (<see cref="TestAuthApiFactory"/>).
/// It proves the pieces work together: routing, validation, role rules, the courier ownership check, the unit of work,
/// the unique ScanId index and the JSON contract. The real-JWT tests in Security/ stay as the proof of token handling.
/// </summary>
[Trait("Category", "Integration")]
public sealed class ShipmentLifecycleEndToEndTests(TestAuthApiFactory factory) : IClassFixture<TestAuthApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ShipmentLifecycle_CreateGetUploadScansAndReadHistory_ReturnsExpectedStatusAndCounts()
    {
        // Arrange
        using var admin = factory.ClientAs(ApiRoles.Admin);
        using var courier = factory.ClientAs(ApiRoles.Courier);
        var trackingNumber = ApiClient.NewTrackingNumber();
        var pickup = Scan(trackingNumber, ScanType.Pickup, minutesAgo: 60);
        var inTransit = Scan(trackingNumber, ScanType.InTransit, minutesAgo: 30);
        var scansPath = $"{ApiClient.V2Shipments}/{trackingNumber}/scans";

        // Act
        using var created = await admin.PostAsJsonAsync(ApiClient.V2Shipments, NewShipment(trackingNumber), Ct);
        using var fetched = await courier.GetAsync(created.Headers.Location, Ct);
        using var upload = await courier.PostAsJsonAsync(scansPath, Batch(pickup, inTransit), Ct);
        using var again = await courier.PostAsJsonAsync(scansPath, Batch(pickup, inTransit), Ct);
        using var history = await courier.GetAsync(scansPath, Ct);
        using var after = await courier.GetAsync($"{ApiClient.V2Shipments}/{trackingNumber}", Ct);

        // Assert
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal($"/api/v2/shipments/{trackingNumber}", created.Headers.Location?.AbsolutePath);
        Assert.Equal(HttpStatusCode.OK, fetched.StatusCode);
        var shipment = await fetched.Content.ReadFromJsonAsync<ShipmentResponse>(ApiClient.Web, Ct);
        Assert.Equal(ShipmentStatus.Created, shipment?.Status);
        Assert.Equal("Harbour Point", shipment?.Recipient.City);
        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
        var first = await upload.Content.ReadFromJsonAsync<ScanUploadResult>(ApiClient.Web, Ct);
        Assert.Equal((2, 0, 0), (first!.Accepted, first.DuplicatesSkipped, first.Rejected.Count));
        var second = await again.Content.ReadFromJsonAsync<ScanUploadResult>(ApiClient.Web, Ct);
        Assert.Equal((0, 2, 0), (second!.Accepted, second.DuplicatesSkipped, second.Rejected.Count));
        Assert.Equal(HttpStatusCode.OK, history.StatusCode);
        var scans = await history.Content.ReadFromJsonAsync<List<ScanResponse>>(ApiClient.Web, Ct);
        Assert.Equal([pickup.ScanId, inTransit.ScanId], scans!.Select(scan => scan.ScanId));
        Assert.All(scans!, scan => Assert.Equal(TestAuthHandler.CourierId, scan.CourierId));
        var latest = await after.Content.ReadFromJsonAsync<ShipmentResponse>(ApiClient.Web, Ct);
        Assert.Equal(ShipmentStatus.InTransit, latest?.Status);
        Assert.Equal(ScanType.InTransit, latest?.LatestScan?.ScanType);
    }

    [Fact]
    public async Task CreateShipment_WithoutTestSchemeCredentials_Returns401ProblemDetails()
    {
        // Arrange
        using var anonymous = factory.ClientAs();
        var trackingNumber = ApiClient.NewTrackingNumber();

        // Act
        using var response = await anonymous.PostAsJsonAsync(ApiClient.V2Shipments, NewShipment(trackingNumber), Ct);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(ApiClient.Web, Ct);
        Assert.Equal(StatusCodes.Status401Unauthorized, problem?.Status);
        await AssertShipmentMissingAsync(trackingNumber);
    }

    [Fact]
    public async Task CreateShipment_CourierRoleInsteadOfAdmin_Returns403ProblemDetails()
    {
        // Arrange
        using var courier = factory.ClientAs(ApiRoles.Courier);
        var trackingNumber = ApiClient.NewTrackingNumber();

        // Act
        using var response = await courier.PostAsJsonAsync(ApiClient.V2Shipments, NewShipment(trackingNumber), Ct);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(ApiClient.Web, Ct);
        Assert.Equal(StatusCodes.Status403Forbidden, problem?.Status);
        await AssertShipmentMissingAsync(trackingNumber);
    }

    private async Task AssertShipmentMissingAsync(string trackingNumber)
    {
        using var admin = factory.ClientAs(ApiRoles.Admin);
        using var response = await admin.GetAsync($"{ApiClient.V2Shipments}/{trackingNumber}", Ct);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static CreateShipmentRequest NewShipment(string trackingNumber) => new()
    {
        TrackingNumber = trackingNumber,
        Recipient = new RecipientRequest
        {
            Name = "Ava Example",
            Phone = "+44 20 7946 0101",
            Address = "12 Quay Street",
            City = "Harbour Point",
        },
        WeightKg = 2.5m,
    };

    private static ScanItem Scan(string trackingNumber, ScanType type, int minutesAgo) => new()
    {
        ScanId = ApiClient.NewScanId(),
        TrackingNumber = trackingNumber,
        ScanType = type,
        CourierId = TestAuthHandler.CourierId,
        Location = "Central Hub",
        ScannedAtUtc = ApiFactory.FixedNow.UtcDateTime.AddMinutes(-minutesAgo),
    };

    private static ScanUploadRequest Batch(params ScanItem[] scans) => new() { Scans = scans };
}
