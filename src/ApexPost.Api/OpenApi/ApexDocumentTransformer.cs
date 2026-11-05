using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace ApexPost.Api.OpenApi;

/// <summary>
/// Fills the <c>info</c> block of every OpenAPI document the built-in generator produces: title, description,
/// version (the document name, for example <c>v1</c>) and the contact of the API team.
/// </summary>
public sealed class ApexDocumentTransformer : IOpenApiDocumentTransformer
{
    /// <summary>The title of the API in the document and in Swagger UI.</summary>
    public const string Title = "ApexPost Logistics API";

    /// <summary>The short description shown under the title.</summary>
    public const string Description =
        "Shipment tracking for ApexPost couriers and partners: create and track shipments, move them through the " +
        "status lifecycle Created → PickedUp → InTransit → OutForDelivery → Delivered | Failed, and upload courier " +
        "scans idempotently. Every error is an RFC 7807 ProblemDetails response.";

    /// <summary>The name of the team that owns the API.</summary>
    public const string ContactName = "ApexPost API Team";

    /// <summary>The fictional mailbox of the API team.</summary>
    public const string ContactEmail = "api-team@apexpost.example";

    /// <inheritdoc />
    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(context);

        document.Info = new OpenApiInfo
        {
            Title = Title,
            Description = Description,
            Version = context.DocumentName,
            Contact = new OpenApiContact { Name = ContactName, Email = ContactEmail },
        };
        return Task.CompletedTask;
    }
}
