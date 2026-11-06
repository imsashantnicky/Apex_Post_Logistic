using System.Text.Json;
using System.Text.Json.Serialization;

namespace ApexPost.Client;

/// <summary>
/// Hand-written additions to the NSwag-generated <see cref="ApexPostApiClient"/>. The generated file is never edited;
/// it calls the partial methods below.
/// </summary>
public partial class ApexPostApiClient
{
    /// <summary>
    /// The v2 API sends and expects enums by name ("InTransit"), but the generated System.Text.Json options would write
    /// them as numbers and fail to read the names. Called once when the shared serializer options are created.
    /// </summary>
    static partial void UpdateJsonSerializerSettings(JsonSerializerOptions settings)
    {
        settings.Converters.Add(new JsonStringEnumConverter());
    }
}
