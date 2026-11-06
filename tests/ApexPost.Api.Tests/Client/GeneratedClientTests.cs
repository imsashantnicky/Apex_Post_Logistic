using System.Net;
using System.Reflection;
using System.Text.Json;
using ApexPost.Api.Tests.Support;
using ApexPost.Client;
using ApexPost.Client.Consumer;

namespace ApexPost.Api.Tests.Client;

/// <summary>
/// The NSwag client generated from contracts/v2.json talks to the real API host: the consumer scenario runs end to
/// end, enums travel by name, errors arrive as <see cref="ApiException"/> with the status code, and the client still
/// has one method per operation of the committed contract.
/// </summary>
public sealed class GeneratedClientTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string Seeded = "APX-20000001";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Consumer_scenario_creates_fetches_uploads_two_scans_and_reports_the_unknown_shipment()
    {
        using var http = factory.CreateClient();
        using var output = new StringWriter();
        var demo = new ShipmentDemo(new ApexPostApiClient(http), output, TimeProvider.System);

        var trackingNumber = await demo.RunAsync(Ct);

        var text = output.ToString();
        Assert.Contains("accepted 2, duplicates skipped 0, rejected 0", text, StringComparison.Ordinal);
        Assert.Contains(ShipmentDemo.NotFoundMessage(ShipmentDemo.UnknownTrackingNumber), text, StringComparison.Ordinal);
        var client = new ApexPostApiClient(http);
        var shipment = await client.GetShipmentAsync(trackingNumber, Ct);
        Assert.Equal(ShipmentStatus.InTransit, shipment.Status);
        Assert.Equal(ScanType.InTransit, shipment.LatestScan?.ScanType);
        Assert.Equal("Harbour Point", shipment.Recipient.City);
        var scans = await client.GetScansAsync(trackingNumber, Ct);
        Assert.Equal([ScanType.Pickup, ScanType.InTransit], scans.Select(scan => scan.ScanType));
        Assert.All(scans, scan => Assert.Matches("^scan::[0-9a-f-]{36}$", scan.ScanId));
    }

    [Fact]
    public async Task Unknown_tracking_number_raises_ApiException_with_404_and_problem_details()
    {
        using var http = factory.CreateClient();
        var client = new ApexPostApiClient(http);

        var exception = await Assert.ThrowsAsync<ApiException<ProblemDetails>>(
            () => client.GetShipmentAsync(ShipmentDemo.UnknownTrackingNumber, Ct));

        Assert.Equal((int)HttpStatusCode.NotFound, exception.StatusCode);
        Assert.Equal((int)HttpStatusCode.NotFound, exception.Result.Status);
    }

    [Fact]
    public async Task Describe_returns_the_status_of_a_known_shipment_and_a_friendly_message_for_404()
    {
        using var http = factory.CreateClient();
        var demo = new ShipmentDemo(new ApexPostApiClient(http), TextWriter.Null, TimeProvider.System);

        var known = await demo.DescribeAsync(Seeded, Ct);
        var unknown = await demo.DescribeAsync(ShipmentDemo.UnknownTrackingNumber, Ct);

        Assert.StartsWith(Seeded + " is ", known, StringComparison.Ordinal);
        Assert.Equal(ShipmentDemo.NotFoundMessage(ShipmentDemo.UnknownTrackingNumber), unknown);
    }

    [Fact]
    public async Task Duplicate_create_raises_ApiException_with_409()
    {
        using var http = factory.CreateClient();
        var client = new ApexPostApiClient(http);
        var existing = await client.GetShipmentAsync(Seeded, Ct);

        var exception = await Assert.ThrowsAnyAsync<ApiException>(() => client.CreateShipmentAsync(
            new CreateShipmentRequest
            {
                TrackingNumber = Seeded,
                WeightKg = 1,
                Recipient = new RecipientRequest
                {
                    Name = existing.Recipient.Name,
                    Phone = existing.Recipient.Phone,
                    Address = existing.Recipient.Address,
                    City = existing.Recipient.City,
                },
            },
            Ct));

        Assert.Equal((int)HttpStatusCode.Conflict, exception.StatusCode);
    }

    [Fact]
    public async Task Status_change_sends_the_enum_by_name_as_v2_expects()
    {
        using var http = factory.CreateClient();
        var client = new ApexPostApiClient(http);
        var created = await new ShipmentDemo(client, TextWriter.Null, TimeProvider.System).RunAsync(Ct);

        var moved = await client.ChangeShipmentStatusAsync(
            created, new UpdateStatusRequest { Status = ShipmentStatus.OutForDelivery }, Ct);

        Assert.Equal(ShipmentStatus.OutForDelivery, moved.Status);
    }

    [Fact]
    public void Client_has_one_async_method_per_operation_of_the_committed_v2_contract()
    {
        using var contract = JsonDocument.Parse(File.ReadAllText(ContractPath()));
        var operationIds = contract.RootElement.GetProperty("paths").EnumerateObject()
            .SelectMany(path => path.Value.EnumerateObject())
            .Select(operation => operation.Value.GetProperty("operationId").GetString() + "Async")
            .Order(StringComparer.Ordinal);

        var methods = typeof(IApexPostApiClient).GetMethods().Select(method => method.Name).Distinct()
            .Order(StringComparer.Ordinal);

        Assert.Equal(operationIds, methods);
    }

    [Fact]
    public void Generated_code_is_marked_as_generated()
    {
        Assert.NotNull(typeof(ApexPostApiClient).GetCustomAttribute<System.CodeDom.Compiler.GeneratedCodeAttribute>());
        var source = File.ReadAllText(Path.Combine(
            RepositoryRoot(), "src", "ApexPost.Client", "Generated", "ApexPostApiClient.g.cs"));
        Assert.Contains("<auto-generated>", source, StringComparison.Ordinal);
    }

    private static string ContractPath() => Path.Combine(RepositoryRoot(), "contracts", "v2.json");

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Apex_Post_Logistic.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("The repository root was not found.");
    }
}
