using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ApexPost.Api.Contracts.V1;
using ApexPost.Api.Tests.Support;
using ApexPost.Domain.Tracking;
using static ApexPost.Api.Tests.Support.ApiClient;

namespace ApexPost.Api.Tests.Endpoints;

/// <summary>Every status code of the shipment endpoints, through the real host and a real SQL Server.</summary>
public sealed class ShipmentEndpointTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string SeededCreated = "APX-20000001";
    private const string SeededDelivered = "APX-20000008";
    private const string Unknown = "APX-29999999";
    private const string TextPlain = "text/plain";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task List_returns_items_and_paging_with_the_default_page_size()
    {
        using var client = factory.CreateClient();

        var page = await client.GetFromJsonAsync<PagedResponse<ShipmentResponse>>(Shipments, Ct);

        Assert.NotNull(page);
        Assert.Equal(1, page.Page);
        Assert.Equal(20, page.PageSize);
        Assert.True(page.TotalCount >= 10);
        Assert.Equal((page.TotalCount + 19) / 20, page.TotalPages);
        Assert.NotEmpty(page.Items);
    }

    [Fact]
    public async Task List_filters_by_status_and_city_and_pages()
    {
        using var client = factory.CreateClient();

        var delivered = await client.GetFromJsonAsync<PagedResponse<ShipmentResponse>>(
            $"{Shipments}?status=Delivered&pageSize=1&page=2",
            Ct);
        var hillcrest = await client.GetFromJsonAsync<PagedResponse<ShipmentResponse>>(
            $"{Shipments}?city=hillcrest",
            Ct);

        Assert.Equal((2, 1, 2), (delivered!.Page, delivered.PageSize, delivered.TotalPages));
        Assert.Equal(ShipmentStatus.Delivered, Assert.Single(delivered.Items).Status);
        Assert.Equal(SeededDelivered, Assert.Single(hillcrest!.Items).TrackingNumber);
    }

    [Fact]
    public async Task List_accepts_the_largest_page_size()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"{Shipments}?pageSize=50", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("pageSize=51", "pageSize")]
    [InlineData("pageSize=0", "pageSize")]
    [InlineData("page=0", "page")]
    [InlineData("status=Lost", "status")]
    public async Task List_with_a_bad_query_returns_400(string query, string field)
    {
        using var client = factory.CreateClient();

        var problem = await Problem.ReadAsync(
            await client.GetAsync($"{Shipments}?{query}", Ct),
            HttpStatusCode.BadRequest);

        Assert.True(problem.Root.GetProperty("errors").TryGetProperty(field, out _));
    }

    [Fact]
    public async Task Get_returns_200_with_the_shipment()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"{Shipments}/{SeededCreated}", Ct);
        var shipment = await response.Content.ReadFromJsonAsync<ShipmentResponse>(Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(SeededCreated, shipment!.TrackingNumber);
    }

    [Theory]
    [InlineData(Unknown)]
    [InlineData("APX-1")]
    [InlineData("XYZ-20000001")]
    public async Task Get_of_an_unknown_or_malformed_tracking_number_returns_404(string trackingNumber)
    {
        using var client = factory.CreateClient();

        await Problem.ReadAsync(await client.GetAsync($"{Shipments}/{trackingNumber}", Ct), HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Create_returns_201_with_a_Location_that_can_be_read()
    {
        using var client = factory.CreateClient();
        var request = Requests.Create();
        var body = new
        {
            trackingNumber = NewTrackingNumber(),
            request.RecipientName,
            request.RecipientPhone,
            request.RecipientAddress,
            request.RecipientCity,
            request.WeightKg,
        };

        var response = await client.PostAsJsonAsync(Shipments, body, Ct);
        var created = await response.Content.ReadFromJsonAsync<ShipmentResponse>(Ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(ShipmentStatus.Created, created!.Status);
        Assert.Equal($"{Shipments}/{body.trackingNumber}", response.Headers.Location?.AbsolutePath);
        var read = await client.GetFromJsonAsync<ShipmentResponse>(response.Headers.Location, Ct);
        Assert.Equal(body.trackingNumber, read!.TrackingNumber);
    }

    [Fact]
    public async Task Create_with_an_invalid_body_returns_400()
    {
        using var client = factory.CreateClient();

        var problem = await Problem.ReadAsync(
            await client.PostAsJsonAsync(Shipments, new { trackingNumber = "APX-1", weightKg = 31 }, Ct),
            HttpStatusCode.BadRequest);

        Assert.True(problem.Root.GetProperty("errors").TryGetProperty("TrackingNumber", out _));
    }

    [Fact]
    public async Task Create_with_a_tracking_number_in_use_returns_409()
    {
        using var client = factory.CreateClient();
        var trackingNumber = await CreateShipmentAsync(client, Ct);
        var request = Requests.Create();
        var body = new
        {
            trackingNumber,
            request.RecipientName,
            request.RecipientPhone,
            request.RecipientAddress,
            request.RecipientCity,
            request.WeightKg,
        };

        var problem = await Problem.ReadAsync(
            await client.PostAsJsonAsync(Shipments, body, Ct),
            HttpStatusCode.Conflict);

        Assert.Equal("Duplicate", problem.Title);
    }

    [Fact]
    public async Task Create_with_a_wrong_content_type_returns_415()
    {
        using var client = factory.CreateClient();
        var json = JsonSerializer.Serialize(Requests.Create(), Web);

        await Problem.ReadAsync(
            await client.PostAsync(Shipments, Body(json, TextPlain), Ct),
            HttpStatusCode.UnsupportedMediaType);
    }

    [Fact]
    public async Task Put_returns_200_with_the_new_values()
    {
        using var client = factory.CreateClient();
        var trackingNumber = await CreateShipmentAsync(client, Ct);

        var response = await client.PutAsJsonAsync($"{Shipments}/{trackingNumber}", Requests.Update(), Ct);
        var updated = await response.Content.ReadFromJsonAsync<ShipmentResponse>(Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(("Northgate", 1m), (updated!.RecipientCity, updated.WeightKg));
    }

    [Fact]
    public async Task Put_with_an_invalid_body_returns_400()
    {
        using var client = factory.CreateClient();
        var trackingNumber = await CreateShipmentAsync(client, Ct);

        var problem = await Problem.ReadAsync(
            await client.PutAsJsonAsync($"{Shipments}/{trackingNumber}", new { recipientName = "" }, Ct),
            HttpStatusCode.BadRequest);

        Assert.True(problem.Root.GetProperty("errors").TryGetProperty("RecipientName", out _));
    }

    [Fact]
    public async Task Put_of_an_unknown_shipment_returns_404()
    {
        using var client = factory.CreateClient();

        await Problem.ReadAsync(
            await client.PutAsJsonAsync($"{Shipments}/{Unknown}", Requests.Update(), Ct),
            HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Put_of_a_delivered_shipment_returns_409()
    {
        using var client = factory.CreateClient();

        await Problem.ReadAsync(
            await client.PutAsJsonAsync($"{Shipments}/{SeededDelivered}", Requests.Update(), Ct),
            HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Put_with_a_wrong_content_type_returns_415()
    {
        using var client = factory.CreateClient();
        var json = JsonSerializer.Serialize(Requests.Update(), Web);

        await Problem.ReadAsync(
            await client.PutAsync($"{Shipments}/{SeededCreated}", Body(json, TextPlain), Ct),
            HttpStatusCode.UnsupportedMediaType);
    }

    [Fact]
    public async Task Patch_status_forward_returns_200_with_the_new_status()
    {
        using var client = factory.CreateClient();
        var trackingNumber = await CreateShipmentAsync(client, Ct);

        var response = await client.PatchAsJsonAsync(
            $"{Shipments}/{trackingNumber}/status",
            new { status = ShipmentStatus.PickedUp },
            Ct);
        var moved = await response.Content.ReadFromJsonAsync<ShipmentResponse>(Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(ShipmentStatus.PickedUp, moved!.Status);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"status\":99}")]
    public async Task Patch_status_without_a_valid_status_returns_400(string json)
    {
        using var client = factory.CreateClient();

        await Problem.ReadAsync(
            await client.PatchAsync($"{Shipments}/{SeededCreated}/status", Body(json, "application/json"), Ct),
            HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Patch_status_of_an_unknown_shipment_returns_404()
    {
        using var client = factory.CreateClient();

        await Problem.ReadAsync(
            await client.PatchAsJsonAsync($"{Shipments}/{Unknown}/status", new { status = 2 }, Ct),
            HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Patch_status_backward_returns_409_invalid_transition()
    {
        using var client = factory.CreateClient();

        var problem = await Problem.ReadAsync(
            await client.PatchAsJsonAsync(
                $"{Shipments}/{SeededDelivered}/status",
                new { status = ShipmentStatus.InTransit },
                Ct),
            HttpStatusCode.Conflict);

        Assert.Equal("Invalid status transition", problem.Title);
    }

    [Fact]
    public async Task Patch_status_with_a_wrong_content_type_returns_415()
    {
        using var client = factory.CreateClient();

        await Problem.ReadAsync(
            await client.PatchAsync($"{Shipments}/{SeededCreated}/status", Body("{\"status\":2}", TextPlain), Ct),
            HttpStatusCode.UnsupportedMediaType);
    }

    [Fact]
    public async Task Delete_of_a_Created_shipment_returns_204_and_it_is_gone()
    {
        using var client = factory.CreateClient();
        var trackingNumber = await CreateShipmentAsync(client, Ct);

        var response = await client.DeleteAsync($"{Shipments}/{trackingNumber}", Ct);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(0, response.Content.Headers.ContentLength ?? 0);
        await Problem.ReadAsync(await client.GetAsync($"{Shipments}/{trackingNumber}", Ct), HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_of_an_unknown_shipment_returns_404()
    {
        using var client = factory.CreateClient();

        await Problem.ReadAsync(await client.DeleteAsync($"{Shipments}/{Unknown}", Ct), HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_after_Created_returns_409()
    {
        using var client = factory.CreateClient();
        var trackingNumber = await CreateShipmentAsync(client, Ct);
        await MoveAsync(client, trackingNumber, ShipmentStatus.PickedUp, Ct);

        var problem = await Problem.ReadAsync(
            await client.DeleteAsync($"{Shipments}/{trackingNumber}", Ct),
            HttpStatusCode.Conflict);

        Assert.Equal("Shipment cannot change", problem.Title);
    }
}
