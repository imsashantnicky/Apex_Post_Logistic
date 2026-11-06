using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace ApexPost.Api.OpenApi;

/// <summary>
/// Declares the JWT bearer scheme and requires it for every operation of the document, so clients and Swagger UI
/// ("Authorize") know to send Authorization: Bearer. The roles per operation are in each 403 response description.
/// </summary>
public sealed class BearerSecuritySchemeTransformer : IOpenApiDocumentTransformer
{
    /// <summary>The name of the security scheme in components.securitySchemes.</summary>
    public const string SchemeName = "Bearer";

    /// <inheritdoc />
    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes[SchemeName] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "A JWT from dotnet user-jwts or, in Development, POST /dev/token. Roles: Courier, Admin.",
        };
        document.Security =
        [
            new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference(SchemeName, document)] = [] },
        ];
        return Task.CompletedTask;
    }
}
