using System.Text.Json;
using ApexPost.Api.OpenApi;
using ApexPost.Api.Tests.Support;

namespace ApexPost.Api.Tests.Documentation;

/// <summary>
/// <see cref="NullEnumValueTransformer"/>: no enum schema lists null as a value. The v2 ScanItem uses ScanType as a
/// nullable property; without the transformer the shared ScanType component listed null and NSwag generated an
/// empty class instead of the enum for ScanItem.scanType.
/// </summary>
public sealed class EnumSchemaTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(ApiDocumentation.V1DocumentUrl)]
    [InlineData(ApiDocumentation.V2DocumentUrl)]
    public async Task No_enum_schema_lists_null_as_a_value(string url)
    {
        using var client = factory.CreateClient();
        using var document = JsonDocument.Parse(await client.GetStringAsync(url, Ct));

        var nullValues = EnumLists(document.RootElement)
            .SelectMany(values => values.EnumerateArray())
            .Count(value => value.ValueKind == JsonValueKind.Null);

        Assert.Equal(0, nullValues);
    }

    [Fact]
    public async Task V2_scan_type_lists_exactly_the_four_scan_types()
    {
        using var client = factory.CreateClient();
        using var document = JsonDocument.Parse(await client.GetStringAsync(ApiDocumentation.V2DocumentUrl, Ct));

        var values = document.RootElement.GetProperty("components").GetProperty("schemas")
            .GetProperty("ScanType").GetProperty("enum").EnumerateArray().Select(value => value.GetString());

        Assert.Equal(["Pickup", "InTransit", "Delivered", "Failed"], values);
    }

    private static IEnumerable<JsonElement> EnumLists(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (property.NameEquals("enum") && property.Value.ValueKind == JsonValueKind.Array)
                {
                    yield return property.Value;
                }

                foreach (var nested in EnumLists(property.Value))
                {
                    yield return nested;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var nested in element.EnumerateArray().SelectMany(EnumLists))
            {
                yield return nested;
            }
        }
    }
}
