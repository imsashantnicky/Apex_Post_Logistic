using System.Net;
using ApexPost.Courier.ApiClient.Results;
using ApexPost.Courier.Presentation.Tests.Support;
using ApexPost.Courier.Presentation.ViewModels;

namespace ApexPost.Courier.Presentation.Tests.ViewModels;

public sealed class ShipmentLookupViewModelTests
{
    private const string Shipment = """
        {
          "trackingNumber": "APX-20000005",
          "recipient": { "name": "Mia Example", "phone": "+10000000005", "address": "5 Harbour Road", "city": "Northgate" },
          "weightKg": 2.5,
          "status": "InTransit",
          "latestScan": { "scanType": "InTransit", "location": "Central Hub", "scannedAtUtc": "2026-11-05T09:30:00Z" },
          "createdAtUtc": "2026-11-05T08:00:00Z",
          "version": 3
        }
        """;

    private const string NotFoundProblem = """
        {
          "title": "Not Found",
          "status": 404,
          "detail": "Shipment APX-00000000 was not found.",
          "traceId": "00-abc-def-01"
        }
        """;

    private const string ServerProblem = """
        { "title": "Internal Server Error", "status": 500, "detail": "NullReferenceException at line 42", "traceId": "00-500" }
        """;

    [Fact]
    public async Task A_found_shipment_is_described_with_status_city_and_latest_scan()
    {
        var viewModel = new ShipmentLookupViewModel(
            TestApi.Client(new CannedHandler(HttpStatusCode.OK, Shipment, TestApi.Json)))
        {
            TrackingNumber = "apx-20000005",
        };

        await viewModel.LookupCommand.ExecuteAsync();

        Assert.False(viewModel.ResultIsError);
        Assert.Equal("APX-20000005", viewModel.TrackingNumber);
        Assert.Contains("APX-20000005: InTransit", viewModel.Result, StringComparison.Ordinal);
        Assert.Contains("To Northgate", viewModel.Result, StringComparison.Ordinal);
        Assert.Contains("InTransit at Central Hub", viewModel.Result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_404_problem_shows_the_server_detail_and_the_trace_id()
    {
        var viewModel = new ShipmentLookupViewModel(
            TestApi.Client(new CannedHandler(HttpStatusCode.NotFound, NotFoundProblem, TestApi.Problem)))
        {
            TrackingNumber = "APX-00000000",
        };

        await viewModel.LookupCommand.ExecuteAsync();

        Assert.True(viewModel.ResultIsError);
        Assert.Equal(
            $"Shipment APX-00000000 was not found.{Environment.NewLine}Reference: 00-abc-def-01",
            viewModel.Result);
    }

    [Fact]
    public async Task A_500_problem_never_shows_server_internals()
    {
        var viewModel = new ShipmentLookupViewModel(
            TestApi.Client(new CannedHandler(HttpStatusCode.InternalServerError, ServerProblem, TestApi.Problem)))
        {
            TrackingNumber = "APX-20000005",
        };

        await viewModel.LookupCommand.ExecuteAsync();

        Assert.True(viewModel.ResultIsError);
        Assert.DoesNotContain("NullReferenceException", viewModel.Result, StringComparison.Ordinal);
        Assert.Contains("Reference: 00-500", viewModel.Result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_malformed_tracking_number_is_refused_without_a_request()
    {
        var handler = new CannedHandler(HttpStatusCode.OK, Shipment, TestApi.Json);
        var viewModel = new ShipmentLookupViewModel(TestApi.Client(handler)) { TrackingNumber = "APX-1" };

        await viewModel.LookupCommand.ExecuteAsync();

        Assert.Empty(handler.Requests);
        Assert.Equal(FailureMessages.InvalidTrackingNumber, viewModel.Result);
    }
}
