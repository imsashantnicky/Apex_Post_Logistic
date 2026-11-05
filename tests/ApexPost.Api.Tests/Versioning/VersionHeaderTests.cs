using System.Net;
using ApexPost.Api.Tests.Support;
using static ApexPost.Api.Tests.Support.ApiClient;

namespace ApexPost.Api.Tests.Versioning;

/// <summary>ReportApiVersions: every versioned answer names the supported and the deprecated versions.</summary>
public sealed class VersionHeaderTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string Supported = "api-supported-versions";
    private const string Deprecated = "api-deprecated-versions";
    private const string Seeded = "/APX-20000001";
    private const string Unknown = "/APX-29999999";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static TheoryData<string> Routes =>
    [
        Shipments + Seeded, V1Shipments + Seeded, V2Shipments + Seeded, V2Shipments,
        V2Shipments + Seeded + "/scans", V1Shipments + Unknown,
    ];

    [Theory]
    [MemberData(nameof(Routes))]
    public async Task Answer_reports_v2_supported_and_v1_deprecated(string route)
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(route, Ct);

        Assert.Equal("2.0", Assert.Single(response.Headers.GetValues(Supported)));
        Assert.Equal("1.0", Assert.Single(response.Headers.GetValues(Deprecated)));
    }

    [Fact]
    public async Task Request_without_a_version_is_served_by_v1()
    {
        using var client = factory.CreateClient();

        var alias = await client.GetStringAsync(Shipments + Seeded, Ct);
        var v1 = await client.GetStringAsync(V1Shipments + Seeded, Ct);

        Assert.Equal(v1, alias);
    }

    [Fact]
    public async Task Unknown_version_is_404_problem_details()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/v3/shipments" + Seeded, Ct);

        await Problem.ReadAsync(response, HttpStatusCode.NotFound);
    }
}
