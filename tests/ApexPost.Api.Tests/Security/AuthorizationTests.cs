using System.Net;
using System.Net.Http.Json;
using ApexPost.Api.Contracts.V1;
using ApexPost.Api.Security;
using ApexPost.Api.Tests.Support;
using ApexPost.Domain.Tracking;
using static ApexPost.Api.Tests.Support.ApiClient;

namespace ApexPost.Api.Tests.Security;

/// <summary>
/// Role rules: scan upload and status PATCH need Courier, create/replace/DELETE need Admin; a wrong role is 403
/// ProblemDetails. A courier may upload only its own scans: the tracking service compares each scan's courierId
/// with the token's courierId claim and answers 403 for any other.
/// </summary>
public sealed class AuthorizationTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string CourierToken(string? courierId = TestTokens.Courier) =>
        TestTokens.Create([ApiRoles.Courier], courierId);

    private static string AdminToken() => TestTokens.Create([ApiRoles.Admin], courierId: null);

    [Theory]
    [InlineData(Shipments)]
    [InlineData(V1Shipments)]
    public async Task Admin_cannot_upload_scans_and_gets_403_problem_details(string baseUrl)
    {
        var trackingNumber = await NewShipmentAsync();
        using var admin = factory.ClientWith(AdminToken());

        var response = await admin.PostAsJsonAsync(
            $"{baseUrl}/{trackingNumber}/scans",
            new ScanUploadRequest { Scans = [Scan(trackingNumber, ScanType.Pickup)] },
            Ct);

        var problem = await Problem.ReadAsync(response, HttpStatusCode.Forbidden);
        Assert.Equal(BearerProblemEvents.ForbiddenDetail, problem.Detail);
        Assert.NotNull(problem.TraceId);
    }

    [Fact]
    public async Task Admin_cannot_change_the_status_in_v2()
    {
        var trackingNumber = await NewShipmentAsync();
        using var admin = factory.ClientWith(AdminToken());

        var response = await admin.PatchAsJsonAsync(
            $"{V2Shipments}/{trackingNumber}/status",
            new { status = "PickedUp" },
            Ct);

        await Problem.ReadAsync(response, HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Courier_cannot_create_replace_or_delete_shipments()
    {
        var trackingNumber = await NewShipmentAsync();
        using var courier = factory.ClientWith(CourierToken());
        var created = await courier.PostAsJsonAsync(V1Shipments, Requests.Create(), Ct);
        var replaced = await courier.PutAsJsonAsync($"{V1Shipments}/{trackingNumber}", Requests.Update(), Ct);
        var deleted = await courier.DeleteAsync($"{V2Shipments}/{trackingNumber}", Ct);

        await Problem.ReadAsync(created, HttpStatusCode.Forbidden);
        await Problem.ReadAsync(replaced, HttpStatusCode.Forbidden);
        var problem = await Problem.ReadAsync(deleted, HttpStatusCode.Forbidden);
        Assert.Equal(BearerProblemEvents.ForbiddenDetail, problem.Detail);
    }

    [Fact]
    public async Task Courier_uploads_its_own_scans_and_moves_the_status()
    {
        var trackingNumber = await NewShipmentAsync();
        using var courier = factory.ClientWith(CourierToken());

        var upload = await courier.PostAsJsonAsync(
            $"{V1Shipments}/{trackingNumber}/scans",
            new ScanUploadRequest { Scans = [Scan(trackingNumber, ScanType.Pickup)] },
            Ct);
        var patch = await courier.PatchAsJsonAsync(
            $"{V2Shipments}/{trackingNumber}/status",
            new { status = "InTransit" },
            Ct);

        Assert.Equal((HttpStatusCode.OK, HttpStatusCode.OK), (upload.StatusCode, patch.StatusCode));
        Assert.Equal(1, (await upload.Content.ReadFromJsonAsync<ScanUploadResult>(Ct))!.Accepted);
    }

    [Fact]
    public async Task Admin_deletes_a_created_shipment()
    {
        var trackingNumber = await NewShipmentAsync();
        using var admin = factory.ClientWith(AdminToken());

        var response = await admin.DeleteAsync($"{V2Shipments}/{trackingNumber}", Ct);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Courier_cannot_upload_a_scan_of_another_courier_and_nothing_is_stored()
    {
        var trackingNumber = await NewShipmentAsync();
        using var courier = factory.ClientWith(CourierToken(TestTokens.Courier));
        var own = Scan(trackingNumber, ScanType.Pickup, minutesAgo: 30);
        var template = Scan(trackingNumber, ScanType.InTransit, minutesAgo: 20);
        var foreign = new ScanItem
        {
            ScanId = template.ScanId,
            TrackingNumber = trackingNumber,
            ScanType = template.ScanType,
            CourierId = TestTokens.OtherCourier,
            Location = template.Location,
            ScannedAtUtc = template.ScannedAtUtc,
        };

        var response = await courier.PostAsJsonAsync(
            $"{V1Shipments}/{trackingNumber}/scans",
            new ScanUploadRequest { Scans = [own, foreign] },
            Ct);

        var problem = await Problem.ReadAsync(response, HttpStatusCode.Forbidden);
        Assert.Contains(TestTokens.OtherCourier, problem.Detail, StringComparison.Ordinal);
        Assert.Contains(foreign.ScanId, problem.Detail, StringComparison.Ordinal);
        using var reader = factory.CreateClient();
        var scans = await reader.GetFromJsonAsync<List<ScanResponse>>($"{V1Shipments}/{trackingNumber}/scans", Ct);
        Assert.Empty(scans!);
    }

    [Fact]
    public async Task Courier_of_another_id_cannot_upload_in_v2_either()
    {
        var trackingNumber = await NewShipmentAsync();
        using var courier = factory.ClientWith(CourierToken(TestTokens.OtherCourier));
        var body = new
        {
            scans = new[]
            {
                new
                {
                    scanId = NewScanId(),
                    trackingNumber,
                    scanType = "Pickup",
                    courierId = TestTokens.Courier,
                    location = "Central Hub",
                    scannedAtUtc = DateTime.UtcNow.AddMinutes(-10),
                },
            },
        };

        var response = await courier.PostAsJsonAsync($"{V2Shipments}/{trackingNumber}/scans", body, Ct);

        await Problem.ReadAsync(response, HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Courier_token_without_a_courierId_claim_cannot_upload()
    {
        var trackingNumber = await NewShipmentAsync();
        using var courier = factory.ClientWith(CourierToken(courierId: null));

        var response = await courier.PostAsJsonAsync(
            $"{V1Shipments}/{trackingNumber}/scans",
            new ScanUploadRequest { Scans = [Scan(trackingNumber, ScanType.Pickup)] },
            Ct);

        await Problem.ReadAsync(response, HttpStatusCode.Forbidden);
    }

    private async Task<string> NewShipmentAsync()
    {
        using var client = factory.CreateClient();
        return await CreateShipmentAsync(client, Ct);
    }
}
