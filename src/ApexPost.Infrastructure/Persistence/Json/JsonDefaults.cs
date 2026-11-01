using System.Text.Json;
using System.Text.Json.Serialization;

namespace ApexPost.Infrastructure.Persistence.Json;

/// <summary>
/// Serializer options for every data file: enums as strings, and strict reading so that a duplicate property,
/// a missing required property or a null where none is allowed is reported instead of guessed.
/// </summary>
public static class JsonDefaults
{
    /// <summary>Indented JSON for the data files, so they stay readable in a code review.</summary>
    public static JsonSerializerOptions Files { get; } = Create(writeIndented: true);

    /// <summary>One entry per line, for the append-only audit log.</summary>
    public static JsonSerializerOptions Lines { get; } = Create(writeIndented: false);

    private static JsonSerializerOptions Create(bool writeIndented)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = writeIndented,
            AllowDuplicateProperties = false,
            RespectNullableAnnotations = true,
            RespectRequiredConstructorParameters = true,
        };
        options.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false));
        options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }
}
