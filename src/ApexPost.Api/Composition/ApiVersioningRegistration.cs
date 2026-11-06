using ApexPost.Api.Controllers;
using Asp.Versioning;

namespace ApexPost.Api.Composition;

/// <summary>
/// URL-segment API versioning (/api/v1/..., /api/v2/...). A request without a version, which is only possible on the
/// Part B alias /api/shipments, is treated as v1. Every answer reports the versions in the api-supported-versions and
/// api-deprecated-versions headers.
/// </summary>
public static class ApiVersioningRegistration
{
    /// <summary>The API Explorer group name format: 'v' plus the major version, so the groups are v1 and v2.</summary>
    public const string GroupNameFormat = "'v'V";

    /// <summary>
    /// Registers API versioning for MVC and the versioned API Explorer the OpenAPI documents read. Returns the
    /// versioning builder so the OpenAPI documents can be added per version.
    /// </summary>
    public static IApiVersioningBuilder AddApexApiVersioning(this IServiceCollection services) =>
        services
            .AddApiVersioning(options =>
            {
                options.DefaultApiVersion = ApiVersionParser.Default.Parse(ApiRoutes.Version1);
                options.AssumeDefaultVersionWhenUnspecified = true;
                options.ReportApiVersions = true;
                options.ApiVersionReader = new UrlSegmentApiVersionReader();
            })
            .AddMvc()
            .AddApiExplorer(options =>
            {
                options.GroupNameFormat = GroupNameFormat;
                options.SubstituteApiVersionInUrl = true;
            });
}
