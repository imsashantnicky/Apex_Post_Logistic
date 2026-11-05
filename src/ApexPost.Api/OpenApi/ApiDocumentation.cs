namespace ApexPost.Api.OpenApi;

/// <summary>
/// The OpenAPI document of the built-in generator and the Swagger UI page that reads it. Both are mapped in
/// Development only; hiding them elsewhere is not a security measure, the endpoints protect themselves.
/// </summary>
public static class ApiDocumentation
{
    /// <summary>The name of the first OpenAPI document; it is also its version.</summary>
    public const string V1 = "v1";

    /// <summary>The URL of the v1 document written by <c>MapOpenApi</c> (pattern /openapi/{documentName}.json).</summary>
    public const string V1DocumentUrl = "/openapi/" + V1 + ".json";

    /// <summary>The route prefix of Swagger UI (/swagger).</summary>
    public const string SwaggerUiPrefix = "swagger";

    /// <summary>Registers the built-in OpenAPI generator with <see cref="ApexDocumentTransformer"/>.</summary>
    public static IServiceCollection AddApexOpenApi(this IServiceCollection services) =>
        services.AddOpenApi(V1, options => options.AddDocumentTransformer<ApexDocumentTransformer>());

    /// <summary>
    /// Maps /openapi/v1.json and Swagger UI at /swagger, in Development only. Swagger UI only renders the document
    /// of the built-in generator; it generates nothing itself.
    /// </summary>
    public static WebApplication MapApexDocumentationInDevelopment(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        if (!app.Environment.IsDevelopment())
        {
            return app;
        }

        app.MapOpenApi();
        app.UseSwaggerUI(options =>
        {
            options.RoutePrefix = SwaggerUiPrefix;
            options.SwaggerEndpoint(V1DocumentUrl, $"{ApexDocumentTransformer.Title} {V1}");
            options.DocumentTitle = ApexDocumentTransformer.Title;
        });
        return app;
    }
}
