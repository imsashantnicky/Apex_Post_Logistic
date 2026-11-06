using System.Text.Json;
using System.Text.Json.Serialization;

namespace ApexPost.Courier.ApiClient.Json;

/// <summary>The one set of JSON settings the app uses to talk to the API.</summary>
public static class ApexJson
{
    /// <summary>
    /// The source-generated context bound to the shared options: camelCase names, case-insensitive reading,
    /// enums as strings, unknown members ignored, and <see langword="null"/> or a missing value in a
    /// non-nullable member refused (a <see cref="JsonException"/> the client turns into a typed failure).
    /// </summary>
    public static ApexJsonContext Context { get; } = new(CreateOptions());

    /// <summary>The shared options; they resolve type metadata only from <see cref="ApexJsonContext"/>.</summary>
    public static JsonSerializerOptions Options => Context.Options;

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip,
            RespectNullableAnnotations = true,
            RespectRequiredConstructorParameters = true,
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
