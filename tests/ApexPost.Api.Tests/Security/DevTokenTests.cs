using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ApexPost.Api.Contracts.V1;
using ApexPost.Api.Controllers;
using ApexPost.Api.Security;
using ApexPost.Api.Tests.Support;
using ApexPost.Domain.Tracking;
using Microsoft.IdentityModel.JsonWebTokens;
using static ApexPost.Api.Tests.Support.ApiClient;

namespace ApexPost.Api.Tests.Security;

/// <summary>
/// POST /dev/token signs a one-hour Courier token for the fictional couriers in Development, with the same issuer,
/// audience and key the bearer handler checks; the token is then refused or accepted like any other.
/// </summary>
public sealed class DevTokenTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string Route = "/" + DevTokenController.Route;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(DevelopmentCouriers.First)]
    [InlineData(DevelopmentCouriers.Second)]
    public async Task Fictional_courier_gets_a_signed_courier_token(string courierId)
    {
        using var anonymous = factory.ClientWith(token: null);

        var response = await anonymous.PostAsJsonAsync(Route, new { courierId }, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<DevTokenResponse>(Ct);
        Assert.Equal(("Bearer", courierId), (body!.TokenType, body.CourierId));
        Assert.InRange(body.ExpiresAtUtc, DateTime.UtcNow.AddMinutes(55), DateTime.UtcNow.AddMinutes(65));
        var jwt = new JsonWebToken(body.AccessToken);
        Assert.Equal(TestTokens.Issuer, jwt.Issuer);
        Assert.Contains(TestTokens.Audience, jwt.Audiences);
        Assert.Equal(ApiRoles.Courier, jwt.GetClaim(ApiClaims.Role).Value);
        Assert.Equal(courierId, jwt.GetClaim(ApiClaims.CourierId).Value);
    }

    [Fact]
    public async Task Dev_token_uploads_own_scans_but_cannot_delete()
    {
        using var anonymous = factory.ClientWith(token: null);
        var issued = await anonymous.PostAsJsonAsync(Route, new { courierId = DevelopmentCouriers.First }, Ct);
        var token = (await issued.Content.ReadFromJsonAsync<DevTokenResponse>(Ct))!.AccessToken;
        string trackingNumber;
        using (var setup = factory.CreateClient())
        {
            trackingNumber = await CreateShipmentAsync(setup, Ct);
        }

        using var courier = factory.ClientWith(token);
        var upload = await courier.PostAsJsonAsync(
            $"{V1Shipments}/{trackingNumber}/scans",
            new ScanUploadRequest { Scans = [Scan(trackingNumber, ScanType.Pickup)] },
            Ct);
        var delete = await courier.DeleteAsync($"{V1Shipments}/{trackingNumber}", Ct);

        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
        await Problem.ReadAsync(delete, HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Unknown_courier_gets_401_problem_details()
    {
        using var anonymous = factory.ClientWith(token: null);

        var response = await anonymous.PostAsJsonAsync(Route, new { courierId = "CR-999" }, Ct);

        var problem = await Problem.ReadAsync(response, HttpStatusCode.Unauthorized);
        Assert.Contains(DevelopmentCouriers.First, problem.Detail, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("CR-1")]
    [InlineData("")]
    public async Task Malformed_courier_id_gets_400_validation_problem(string courierId)
    {
        using var anonymous = factory.ClientWith(token: null);

        var response = await anonymous.PostAsJsonAsync(Route, new { courierId }, Ct);

        var problem = await Problem.ReadAsync(response, HttpStatusCode.BadRequest);
        Assert.True(problem.Root.GetProperty("errors").TryGetProperty("CourierId", out _));
    }

    [Fact]
    public async Task Dev_token_endpoint_is_not_in_the_openapi_documents()
    {
        using var client = factory.CreateClient();

        foreach (var version in new[] { "v1", "v2" })
        {
            using var document = JsonDocument.Parse(await client.GetStringAsync($"/openapi/{version}.json", Ct));
            var paths = document.RootElement.GetProperty("paths").EnumerateObject().Select(path => path.Name);
            Assert.DoesNotContain(paths, path => path.Contains(DevTokenController.Route, StringComparison.Ordinal));
        }
    }
}
