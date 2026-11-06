using ApexPost.Api.Security;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace ApexPost.Api.OpenApi;

/// <summary>
/// The security schemes of the OpenAPI documents, so clients and the Swagger UI "Authorize" button know what to send.
/// Every document declares the JWT <see cref="BearerScheme"/> and requires it by default. An operation with
/// <see cref="PartnerApiKeyAttribute"/> requires the <see cref="ApiKeyScheme"/> (X-Api-Key header) instead, and
/// only a document with such an operation (v2) declares that scheme.
/// </summary>
public sealed class SecuritySchemeTransformer : IOpenApiDocumentTransformer, IOpenApiOperationTransformer
{
    /// <summary>The name of the JWT bearer scheme in components.securitySchemes.</summary>
    public const string BearerScheme = "Bearer";

    /// <summary>The name of the partner API key scheme in components.securitySchemes.</summary>
    public const string ApiKeyScheme = PartnerApiKeyFilter.SchemeName;

    /// <summary>Declares the Bearer scheme and makes it the default requirement of every operation.</summary>
    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        SchemesOf(document)[BearerScheme] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "A JWT from dotnet user-jwts or, in Development, POST /dev/token. Roles: Courier, Admin.",
        };
        document.Security = [Requirement(BearerScheme, document)];
        return Task.CompletedTask;
    }

    /// <summary>Gives a partner operation the ApiKey requirement in place of the default Bearer one.</summary>
    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(context);
        var metadata = context.Description.ActionDescriptor.EndpointMetadata;
        if (!metadata.OfType<PartnerApiKeyAttribute>().Any() || context.Document is not { } document)
        {
            return Task.CompletedTask;
        }

        SchemesOf(document)[ApiKeyScheme] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.ApiKey,
            In = ParameterLocation.Header,
            Name = PartnerApiKeyFilter.HeaderName,
            Description = "A partner key, issued per partner and kept in configuration (PartnerKeys section).",
        };
        operation.Security = [Requirement(ApiKeyScheme, document)];
        return Task.CompletedTask;
    }

    private static IDictionary<string, IOpenApiSecurityScheme> SchemesOf(OpenApiDocument document)
    {
        document.Components ??= new OpenApiComponents();
        return document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
    }

    private static OpenApiSecurityRequirement Requirement(string scheme, OpenApiDocument document) =>
        new() { [new OpenApiSecuritySchemeReference(scheme, document)] = [] };
}
