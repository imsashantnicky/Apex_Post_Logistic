using System.Globalization;
using Asp.Versioning.ApiExplorer;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace ApexPost.Api.OpenApi;

/// <summary>
/// Fills the <c>info</c> block of every OpenAPI document the built-in generator produces: title, description,
/// version (the document name, for example <c>v1</c>) and the contact of the API team. A deprecated version gets a
/// deprecation notice (from its deprecation policy) with its sunset date at the end of the description.
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

        var version = context.ApplicationServices.GetRequiredService<IApiVersionDescriptionProvider>()
            .ApiVersionDescriptions.FirstOrDefault(description => description.GroupName == context.DocumentName);
        document.Info = new OpenApiInfo
        {
            Title = Title,
            Description = version?.DeprecationPolicy is null ? Description : Description + DeprecationNotice(version),
            Version = context.DocumentName,
            Contact = new OpenApiContact { Name = ContactName, Email = ContactEmail },
        };

        // The generator would list the URL of the machine that served the document. Without it the committed
        // contracts are the same everywhere, and Swagger UI calls the origin it was loaded from.
        document.Servers = [];
        return Task.CompletedTask;
    }

    /// <summary>The paragraph added to the description of a deprecated version, with its sunset date if any.</summary>
    public static string DeprecationNotice(ApiVersionDescription version)
    {
        ArgumentNullException.ThrowIfNull(version);
        var sunset = version.SunsetPolicy?.Date is { } date
            ? string.Create(CultureInfo.InvariantCulture, $" and will be removed on {date:yyyy-MM-dd} (sunset)")
            : string.Empty;
        return string.Create(
            CultureInfo.InvariantCulture,
            $"\n\n**Deprecated:** {version.GroupName} is deprecated{sunset}. Use the latest version instead; " +
            $"see VERSIONING.md for the migration.");
    }
}
