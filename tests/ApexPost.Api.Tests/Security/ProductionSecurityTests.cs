using System.Net;
using System.Net.Http.Json;
using ApexPost.Api.Controllers;
using ApexPost.Api.Security;
using ApexPost.Api.Tests.Support;

namespace ApexPost.Api.Tests.Security;

/// <summary>Outside Development there is no /dev/token, and tokens are still required and checked.</summary>
public sealed class ProductionSecurityTests(ProductionApiFactory factory) : IClassFixture<ProductionApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Dev_token_endpoint_does_not_exist_in_Production()
    {
        using var anonymous = factory.ClientWith(token: null);

        var response = await anonymous.PostAsJsonAsync(
            "/" + DevTokenController.Route,
            new { courierId = DevelopmentCouriers.First },
            Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Missing_token_returns_401_problem_details_in_Production()
    {
        using var anonymous = factory.ClientWith(token: null);

        var response = await anonymous.GetAsync(ApiClient.V2Shipments, Ct);

        var problem = await Problem.ReadAsync(response, HttpStatusCode.Unauthorized);
        Assert.Equal(BearerProblemEvents.MissingTokenDetail, problem.Detail);
        Assert.False(problem.Has("stackTrace"));
    }
}
