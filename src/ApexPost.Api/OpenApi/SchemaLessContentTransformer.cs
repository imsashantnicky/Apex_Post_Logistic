using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace ApexPost.Api.OpenApi;

/// <summary>
/// Removes response media types that carry no schema. <c>[ProducesJson]</c> on the controllers adds an empty
/// application/json entry to every response, so without this the 204 of DELETE would promise a JSON body and every
/// error would list application/json next to its real application/problem+json.
/// </summary>
public sealed class SchemaLessContentTransformer : IOpenApiOperationTransformer
{
    /// <inheritdoc />
    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);
        foreach (var response in operation.Responses?.Values.OfType<OpenApiResponse>() ?? [])
        {
            if (response.Content is not { } content)
            {
                continue;
            }

            foreach (var mediaType in content.Where(entry => entry.Value.Schema is null).Select(entry => entry.Key)
                         .ToList())
            {
                content.Remove(mediaType);
            }

            if (content.Count == 0)
            {
                response.Content = null;
            }
        }

        return Task.CompletedTask;
    }
}
