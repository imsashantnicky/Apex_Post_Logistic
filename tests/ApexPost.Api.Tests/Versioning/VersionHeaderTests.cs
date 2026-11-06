using System.Globalization;
using System.Net;
using ApexPost.Api.Composition;
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

    [Theory]
    [InlineData(Shipments + Seeded)]
    [InlineData(V1Shipments + Seeded)]
    [InlineData(V1Shipments)]
    public async Task V1_answer_carries_Deprecation_Sunset_and_the_migration_Link(string route)
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(route, Ct);

        var deprecatedAt = DateTimeOffset.FromUnixTimeSeconds(
            long.Parse(Header(response, "Deprecation").TrimStart('@'), CultureInfo.InvariantCulture));
        var sunsetAt = DateTimeOffset.Parse(Header(response, "Sunset"), CultureInfo.InvariantCulture);
        Assert.Equal(VersionLifecycle.AtMidnightUtc(VersionLifecycle.V1DeprecatedOn), deprecatedAt);
        Assert.Equal(VersionLifecycle.AtMidnightUtc(VersionLifecycle.V1SunsetOn), sunsetAt);
        var links = response.Headers.GetValues("Link").ToList();
        Assert.Contains($"<{VersionLifecycle.MigrationGuideUrl}>; rel=\"deprecation\"", links);
        Assert.Contains($"<{VersionLifecycle.MigrationGuideUrl}>; rel=\"sunset\"", links);
    }

    [Fact]
    public async Task V2_answer_has_no_Deprecation_or_Sunset()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(V2Shipments + Seeded, Ct);

        Assert.False(response.Headers.Contains("Deprecation"));
        Assert.False(response.Headers.Contains("Sunset"));
    }

    [Fact]
    public async Task Request_without_a_version_is_served_by_v1()
    {
        using var client = factory.CreateClient();

        var alias = await client.GetStringAsync(Shipments + Seeded, Ct);
        var v1 = await client.GetStringAsync(V1Shipments + Seeded, Ct);

        Assert.Equal(v1, alias);
    }

    private static string Header(HttpResponseMessage response, string name) =>
        Assert.Single(response.Headers.GetValues(name));

    [Fact]
    public async Task Unknown_version_is_404_problem_details()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/v3/shipments" + Seeded, Ct);

        await Problem.ReadAsync(response, HttpStatusCode.NotFound);
    }
}
