using System.Net;
using System.Text.Json;

namespace ApexPost.Api.Tests.Support;

/// <summary>A ProblemDetails response read back as JSON, after checking its status and content type.</summary>
public sealed class Problem
{
    public const string ContentType = "application/problem+json";

    private Problem(JsonElement root) => Root = root;

    public JsonElement Root { get; }

    public string? Title => Text("title");

    public string? Detail => Text("detail");

    public string? TraceId => Text("traceId");

    public bool Has(string property) => Root.TryGetProperty(property, out _);

    public string? Text(string property) =>
        Root.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    public static async Task<Problem> ReadAsync(HttpResponseMessage response, HttpStatusCode expected)
    {
        ArgumentNullException.ThrowIfNull(response);
        Assert.Equal(expected, response.StatusCode);
        Assert.Equal(ContentType, response.Content.Headers.ContentType?.MediaType);

        var json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement.Clone();
        Assert.Equal((int)expected, root.GetProperty("status").GetInt32());
        return new Problem(root);
    }
}
