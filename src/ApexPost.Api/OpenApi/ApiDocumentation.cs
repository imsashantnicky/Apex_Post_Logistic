using Asp.Versioning;
using Asp.Versioning.OpenApi.Transformers;
using Microsoft.AspNetCore.Mvc.ApiExplorer;

namespace ApexPost.Api.OpenApi;

/// <summary>
/// The OpenAPI documents of the built-in generator, one per API version, and the Swagger UI page that reads them.
/// Both are mapped in Development only; hiding them elsewhere is not a security measure, the endpoints protect
/// themselves.
/// </summary>
public static class ApiDocumentation
{
    /// <summary>The name of the v1 document (the API Explorer group of v1); it is also its version.</summary>
    public const string V1 = "v1";

    /// <summary>The name of the v2 document (the API Explorer group of v2); it is also its version.</summary>
    public const string V2 = "v2";

    /// <summary>The URL of the v1 document written by <c>MapOpenApi</c> (pattern /openapi/{documentName}.json).</summary>
    public const string V1DocumentUrl = "/openapi/" + V1 + ".json";

    /// <summary>The Swagger UI drop-down entry of the v1 document.</summary>
    public const string V1DeprecatedLabel = V1 + " (deprecated)";

    /// <summary>The URL of the v2 document.</summary>
    public const string V2DocumentUrl = "/openapi/" + V2 + ".json";

    /// <summary>The start of every versioned route after the version was put into the URL (api/v1/, api/v2/).</summary>
    public const string VersionedPathPrefix = "api/v";

    /// <summary>The route prefix of Swagger UI (/swagger).</summary>
    public const string SwaggerUiPrefix = "swagger";

    /// <summary>
    /// Registers the built-in OpenAPI generator through API Versioning: it calls <c>AddOpenApi("v1")</c> and
    /// <c>AddOpenApi("v2")</c> itself, one document per API Explorer group name. A direct <c>services.AddOpenApi</c>
    /// is rejected by the Asp.Versioning analyzer AV0029.
    /// Each document gets <see cref="ApexDocumentTransformer"/> (info block),
    /// <see cref="SchemaLessContentTransformer"/> (no empty media types) and <see cref="NullEnumValueTransformer"/>
    /// (no null in enum value lists), and lists only its own versioned routes.
    /// The XML comments (GenerateDocumentationFile is on) are read by API Versioning; <see cref="XmlExampleTransformer"/>
    /// adds the property examples and request body descriptions it leaves out.
    /// </summary>
    public static IApiVersioningBuilder AddApexOpenApi(this IApiVersioningBuilder builder)
    {
        var examples = new XmlExampleTransformer(XmlComments.FromFile(XmlExampleTransformer.CommentsFilePath));
        return builder.AddOpenApi(options =>
        {
            var documentName = options.Description.GroupName;
            options.Document.ShouldInclude = description => IsVersionedRouteOf(description, documentName);
            options.Document.AddDocumentTransformer<ApexDocumentTransformer>();
            options.Document.AddOperationTransformer<SchemaLessContentTransformer>();
            options.Document.AddOperationTransformer(examples);
            options.Document.AddSchemaTransformer(examples);
            options.Document.AddSchemaTransformer<NullEnumValueTransformer>();
        });
    }

    /// <summary>
    /// Whether an endpoint belongs in the document of this version: its API Explorer group (v1, v2) matches and its
    /// route is versioned. The unversioned /api/shipments alias runs the same v1 actions, so documenting it would
    /// list every v1 operation twice without an operationId.
    /// </summary>
    public static bool IsVersionedRouteOf(ApiDescription description, string documentName)
    {
        ArgumentNullException.ThrowIfNull(description);
        return string.Equals(description.GroupName, documentName, StringComparison.Ordinal)
            && description.RelativePath?.StartsWith(VersionedPathPrefix, StringComparison.Ordinal) == true;
    }

    /// <summary>
    /// Maps /openapi/v1.json and /openapi/v2.json and Swagger UI at /swagger, in Development only. Swagger UI only
    /// renders the documents of the built-in generator; it generates nothing itself. v2 is listed first.
    /// </summary>
    public static WebApplication MapApexDocumentationInDevelopment(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        if (!app.Environment.IsDevelopment())
        {
            return app;
        }

        app.MapOpenApi().WithDocumentPerVersion();
        app.UseSwaggerUI(options =>
        {
            options.RoutePrefix = SwaggerUiPrefix;
            options.SwaggerEndpoint(V2DocumentUrl, V2);
            options.SwaggerEndpoint(V1DocumentUrl, V1DeprecatedLabel);
            options.DocumentTitle = ApexDocumentTransformer.Title;
        });
        return app;
    }
}
