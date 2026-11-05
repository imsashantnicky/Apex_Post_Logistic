using ApexPost.Api.Contracts.V1;
using ApexPost.Api.Tests.Support;
using ApexPost.Application.DTOs;
using ApexPost.Application.Tracking;
using ApexPost.Domain.Tracking;
using UploadResult = ApexPost.Application.Tracking.ScanUploadResult;

namespace ApexPost.Api.Tests.Contracts;

/// <summary>Every field crosses the mapping unchanged, in both directions.</summary>
public sealed class ShipmentMappingTests
{
    private static readonly DateTime Now = FixedTimeProvider.DefaultNow.UtcDateTime;

    [Fact]
    public void Create_request_becomes_a_command()
    {
        var command = Requests.Create().ToCommand();

        Assert.Equal(
            new ShipmentCommand("Ava Example", "+44 20 7946 0101", "12 Quay Street", "Harbour Point", 2.5m),
            command);
    }

    [Fact]
    public void Update_request_becomes_a_command()
    {
        var command = Requests.Update().ToCommand();

        Assert.Equal(new ShipmentCommand("Ben Sample", "020 7946 0102", "4 Mill Lane", "Northgate", 1m), command);
    }

    [Fact]
    public void Scan_items_become_uploads_in_order()
    {
        var request = new ScanUploadRequest { Scans = [Requests.Scan(), Requests.Scan(ScanType.Delivered)] };

        var uploads = request.ToUploads();

        Assert.Equal(
            new ScanUpload(
                Requests.ScanId,
                "APX-20000001",
                ScanType.Pickup,
                "CR-101",
                "Central Hub",
                Now.AddMinutes(-30),
                "Left at reception"),
            uploads[0]);
        Assert.Equal(ScanType.Delivered, uploads[1].ScanType);
    }

    [Fact]
    public void Shipment_view_becomes_a_flat_v1_response()
    {
        var view = new ShipmentView(
            "APX-20000005", "Eve Placeholder", "+44 20 7946 0105", "17 Runway Close", "Southmere", 7.25m,
            ShipmentStatus.InTransit, Now, 3, Scan());

        var response = view.ToResponse();

        Assert.Equal(
            new ShipmentResponse(
                "APX-20000005", "Eve Placeholder", "+44 20 7946 0105", "17 Runway Close", "Southmere", 7.25m,
                ShipmentStatus.InTransit, Now, 3),
            response);
    }

    [Fact]
    public void Scan_view_becomes_a_response()
    {
        var response = Scan().ToResponse();

        Assert.Equal(
            new ScanResponse(
                Requests.ScanId, "APX-20000005", ScanType.InTransit, "CR-102", "North Depot", Now.AddHours(-1),
                null, Now),
            response);
    }

    [Fact]
    public void Page_keeps_its_paging_numbers()
    {
        var view = new ShipmentView("APX-20000001", "A", "1234567", "B", "C", 1m, ShipmentStatus.Created, Now, 1, null);

        var response = new PagedResult<ShipmentView>([view], 2, 1, 3).ToResponse();

        Assert.Equal("APX-20000001", Assert.Single(response.Items).TrackingNumber);
        Assert.Equal((2, 1, 3, 3, true), (response.Page, response.PageSize, response.TotalCount, response.TotalPages,
            response.HasNext));
    }

    [Fact]
    public void Upload_result_keeps_counts_and_rejections()
    {
        var result = new UploadResult(2, 1, [new RejectedScan(Requests.ScanId, "Unknown tracking number.")]);

        var response = result.ToResponse();

        Assert.Equal(2, response.Accepted);
        Assert.Equal(1, response.DuplicatesSkipped);
        Assert.Equal(
            new RejectedScanResponse(Requests.ScanId, "Unknown tracking number."),
            Assert.Single(response.Rejected));
    }

    private static ScanView Scan() =>
        new(Requests.ScanId, "APX-20000005", ScanType.InTransit, "CR-102", "North Depot", Now.AddHours(-1), null, Now);
}
