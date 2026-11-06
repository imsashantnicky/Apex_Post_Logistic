using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace ApexPost.Api.OpenApi;

/// <summary>
/// Removes <c>null</c> from the value list of enum schemas. The built-in generator adds it when one property uses the
/// enum as nullable (v2 <c>ScanItem.ScanType</c>), so the shared ScanType component would list null as a scan type
/// for every property. Nullability is already stated where it belongs, as <c>oneOf: [null, $ref]</c> on the
/// property. Without this, NSwag turns the nullable reference into an empty class instead of the enum.
/// </summary>
public sealed class NullEnumValueTransformer : IOpenApiSchemaTransformer
{
    /// <inheritdoc />
    public Task TransformAsync(
        OpenApiSchema schema,
        OpenApiSchemaTransformerContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(schema);
        if (schema.Enum is { Count: > 0 } values)
        {
            for (var index = values.Count - 1; index >= 0; index--)
            {
                if (values[index] is null)
                {
                    values.RemoveAt(index);
                }
            }
        }

        return Task.CompletedTask;
    }
}
