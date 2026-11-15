using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ApexPost.Api.Contracts.V1;
using ApexPost.Api.Security;
using ApexPost.Specs.Contexts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Reqnroll;
using V2 = ApexPost.Api.Contracts.V2;

namespace ApexPost.Specs.Steps;

/// <summary>
/// Steps of ShipmentCreation.feature. The business outcome of registering a shipment is the API's answer, so every
/// step goes through HTTP to the real host (<see cref="ShipmentContext.Factory"/>); only the "register is empty"
/// set-up removes the seeded demo shipments directly in the scenario's database.
/// </summary>
[Binding]
public sealed class ShipmentCreationSteps(ShipmentContext context)
{
    private const string Shipments = "/api/v2/shipments";
    private const string DefaultPhone = "+44 20 7946 0101";
    private const string DefaultAddress = "12 Quay Street";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Given("I am signed in as a dispatcher with the {word} role")]
    public void GivenSignedInWithRole(string role)
    {
        // The test scheme signs the caller in with exactly this role; the API's role rules stay real.
        context.Client = context.Factory.ClientAs(role);
    }

    [Given("the shipment register is empty")]
    public async Task GivenTheShipmentRegisterIsEmpty()
    {
        await using (var database = context.Factory.OpenTrackingDatabase())
        {
            await database.Scans.ExecuteDeleteAsync(Ct);
            await database.Shipments.ExecuteDeleteAsync(Ct);
        }

        Assert.Equal(0, await CountShipmentsAsync());
    }

    [Given("shipment {string} is already registered")]
    public async Task GivenShipmentIsAlreadyRegistered(string trackingNumber)
    {
        using var response = await RegisterAsync(trackingNumber, "Ava Example", "Harbour Point", 2.5m);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [When("I register shipment {string} for {string} in {string} weighing {word} kg")]
    public async Task WhenIRegisterShipment(string trackingNumber, string name, string city, string weight)
    {
        var weightKg = decimal.Parse(weight, NumberStyles.Number, CultureInfo.InvariantCulture);
        context.LastResponse = await RegisterAsync(trackingNumber, name, city, weightKg);
    }

    [Then("the shipment is registered")]
    public void ThenTheShipmentIsRegistered()
    {
        var response = LastResponse();
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
    }

    [Then("looking up shipment {string} shows status {string}")]
    public async Task ThenLookingUpShowsStatus(string trackingNumber, string status)
    {
        var shipment = await LookUpAsync(trackingNumber);
        Assert.Equal(status, shipment.Status.ToString());
    }

    [Then("looking up shipment {string} shows recipient {string} in {string}")]
    public async Task ThenLookingUpShowsRecipient(string trackingNumber, string name, string city)
    {
        var shipment = await LookUpAsync(trackingNumber);
        Assert.Equal((name, city), (shipment.Recipient.Name, shipment.Recipient.City));
    }

    [Then("the shipment is refused as a duplicate")]
    public async Task ThenTheShipmentIsRefusedAsADuplicate()
    {
        var response = LastResponse();
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(Json, Ct);
        Assert.Equal("Duplicate", problem?.Title);
    }

    [Then("the shipment is refused with the message {string}")]
    public async Task ThenTheShipmentIsRefusedWithTheMessage(string message)
    {
        var response = LastResponse();
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(Json, Ct);
        Assert.Contains(message, problem!.Errors.SelectMany(error => error.Value));
    }

    [Then("the shipment register holds {int} shipment(s)")]
    public async Task ThenTheShipmentRegisterHolds(int count) => Assert.Equal(count, await CountShipmentsAsync());

    private async Task<HttpResponseMessage> RegisterAsync(
        string trackingNumber,
        string name,
        string city,
        decimal weightKg)
    {
        var request = new V2.CreateShipmentRequest
        {
            TrackingNumber = trackingNumber,
            Recipient = new V2.RecipientRequest
            {
                Name = name,
                Phone = DefaultPhone,
                Address = DefaultAddress,
                City = city,
            },
            WeightKg = weightKg,
        };
        return await context.Client.PostAsJsonAsync(Shipments, request, Ct);
    }

    private async Task<V2.ShipmentResponse> LookUpAsync(string trackingNumber)
    {
        using var response = await context.Client.GetAsync($"{Shipments}/{trackingNumber}", Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var shipment = await response.Content.ReadFromJsonAsync<V2.ShipmentResponse>(Json, Ct);
        return shipment ?? throw new InvalidOperationException("The API returned an empty shipment.");
    }

    private async Task<int> CountShipmentsAsync()
    {
        using var reader = context.Factory.ClientAs(ApiRoles.Admin);
        var page = await reader.GetFromJsonAsync<PagedResponse<V2.ShipmentResponse>>(Shipments, Json, Ct);
        return page?.TotalCount ?? throw new InvalidOperationException("The API returned an empty page.");
    }

    private HttpResponseMessage LastResponse() =>
        context.LastResponse ?? throw new InvalidOperationException("No shipment was registered in this scenario.");
}
