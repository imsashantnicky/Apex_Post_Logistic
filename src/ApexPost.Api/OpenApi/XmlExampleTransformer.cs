using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Asp.Versioning.OpenApi.Transformers;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace ApexPost.Api.OpenApi;

/// <summary>
/// Adds the two parts of the XML comments that API Versioning's OpenAPI integration does not write: the
/// <c>&lt;example&gt;</c> of every contract property and the description of the request body (the
/// <c>&lt;param name="request"&gt;</c> of the action). Summaries and parameter examples are already written by it.
/// </summary>
/// <param name="comments">The XML comments of the API assembly.</param>
public sealed class XmlExampleTransformer(XmlComments comments) : IOpenApiSchemaTransformer, IOpenApiOperationTransformer
{
    /// <summary>The annotation the built-in generator copies to the <c>$ref</c> of a property as its example.</summary>
    public const string RefExampleAnnotation = "x-ref-example";

    /// <summary>The annotation the built-in generator copies to the <c>$ref</c> of a property as its description.</summary>
    public const string RefDescriptionAnnotation = "x-ref-description";

    /// <summary>The XML comments file the build writes next to the API assembly.</summary>
    public static string CommentsFilePath =>
        Path.Combine(AppContext.BaseDirectory, typeof(XmlExampleTransformer).Assembly.GetName().Name + ".xml");

    /// <summary>
    /// Sets the example of every property of a contract schema from its <c>&lt;example&gt;</c>. An enum property ends
    /// up as a <c>$ref</c> (inside <c>oneOf</c> when nullable); its example sits on the reference.
    /// </summary>
    public Task TransformAsync(
        OpenApiSchema schema,
        OpenApiSchemaTransformerContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(context);
        if (context.JsonPropertyInfo is not null || schema.Properties is not { Count: > 0 } properties)
        {
            return Task.CompletedTask;
        }

        foreach (var property in context.JsonTypeInfo.Properties)
        {
            if (property.AttributeProvider is PropertyInfo info
                && properties.TryGetValue(property.Name, out var propertySchema)
                && propertySchema is OpenApiSchema inline
                && ExampleOf(info) is { } example)
            {
                SetExample(inline, IsEnum(property.PropertyType) ? comments.GetSummary(info) : null, example);
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>Sets the request body description from the XML comment of the body parameter.</summary>
    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(context);
        if (operation.RequestBody is not OpenApiRequestBody { Description: null } body)
        {
            return Task.CompletedTask;
        }

        var parameter = context.Description.ParameterDescriptions
            .FirstOrDefault(candidate => candidate.Source == BindingSource.Body)?.ParameterDescriptor;
        if (parameter is ControllerParameterDescriptor { ParameterInfo: var info })
        {
            var description = comments.GetParameterDescription(info.Member, info.Name ?? string.Empty);
            body.Description = string.IsNullOrWhiteSpace(description) ? null : description;
        }

        return Task.CompletedTask;
    }

    // An enum property becomes a $ref to the shared enum schema after the transformers ran. Its example and summary
    // must not go into that shared schema, so they are left as the annotations the generator moves onto the
    // reference (the enum branch of oneOf when the property is nullable).
    private static void SetExample(OpenApiSchema schema, string? enumSummary, JsonNode example)
    {
        if (enumSummary is not null)
        {
            var metadata = schema.Metadata ??= new Dictionary<string, object>();
            metadata[RefExampleAnnotation] = example;
            metadata[RefDescriptionAnnotation] = enumSummary;
        }
        else if (schema.Examples is not { Count: > 0 })
        {
            schema.Examples = [example];
        }
    }

    private static bool IsEnum(Type type) => (Nullable.GetUnderlyingType(type) ?? type).IsEnum;

    // The same reading as the built-in XML comment generator: valid JSON stays JSON (numbers, arrays, booleans),
    // anything else is a string.
    private JsonNode? ExampleOf(PropertyInfo property)
    {
        var example = comments.GetExample(property).Trim();
        if (example.Length == 0)
        {
            return null;
        }

        try
        {
            return JsonNode.Parse(example);
        }
        catch (JsonException)
        {
            return JsonValue.Create(example);
        }
    }
}
