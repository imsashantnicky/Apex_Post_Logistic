using System.Text.Json.Nodes;
using ApexPost.Api.Tests.Support;

namespace ApexPost.Api.Tests.Documentation;

/// <summary>
/// Contract tracking: the live OpenAPI documents must equal the committed /contracts/v1.json and /contracts/v2.json.
/// Any change of a route, DTO, status code or comment fails here until the contracts are exported again with
/// scripts/export-contracts.ps1 (or .sh) and the diff is reviewed in the pull request.
/// </summary>
public sealed class ContractTrackingTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string SolutionFile = "Apex_Post_Logistic.slnx";
    private const string ContractsFolder = "contracts";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("v1")]
    [InlineData("v2")]
    public async Task Live_document_equals_the_committed_contract(string version)
    {
        using var client = factory.CreateClient();

        var live = JsonNode.Parse(await client.GetStringAsync($"/openapi/{version}.json", Ct));
        var committed = JsonNode.Parse(await File.ReadAllTextAsync(ContractPath(version), Ct));

        var difference = FirstDifference(committed, live, "$");
        Assert.True(
            difference is null,
            $"/openapi/{version}.json differs from {ContractsFolder}/{version}.json at {difference}. If the change " +
            "is intended, run scripts/export-contracts.ps1 (or .sh) and commit the new contract.");
    }

    [Fact]
    public void Difference_finder_reports_the_path_of_a_changed_value()
    {
        var committed = JsonNode.Parse("""{"paths":{"/a":{"get":{"operationId":"GetA"}}}}""");
        var live = JsonNode.Parse("""{"paths":{"/a":{"get":{"operationId":"GetB"}}}}""");

        Assert.Equal("$.paths./a.get.operationId", FirstDifference(committed, live, "$"));
        Assert.Null(FirstDifference(committed, committed!.DeepClone(), "$"));
    }

    /// <summary>The JSON path of the first difference, or null when both documents are equal.</summary>
    private static string? FirstDifference(JsonNode? expected, JsonNode? actual, string path)
    {
        if (expected is JsonObject expectedObject && actual is JsonObject actualObject)
        {
            var names = expectedObject.Select(property => property.Key)
                .Union(actualObject.Select(property => property.Key), StringComparer.Ordinal);
            return names
                .Select(name => FirstDifference(expectedObject[name], actualObject[name], $"{path}.{name}"))
                .FirstOrDefault(difference => difference is not null);
        }

        if (expected is JsonArray expectedArray && actual is JsonArray actualArray
            && expectedArray.Count == actualArray.Count)
        {
            return Enumerable.Range(0, expectedArray.Count)
                .Select(index => FirstDifference(expectedArray[index], actualArray[index], $"{path}[{index}]"))
                .FirstOrDefault(difference => difference is not null);
        }

        return JsonNode.DeepEquals(expected, actual) ? null : path;
    }

    private static string ContractPath(string version)
    {
        for (var folder = new DirectoryInfo(AppContext.BaseDirectory); folder is not null; folder = folder.Parent)
        {
            if (File.Exists(Path.Combine(folder.FullName, SolutionFile)))
            {
                return Path.Combine(folder.FullName, ContractsFolder, version + ".json");
            }
        }

        throw new InvalidOperationException($"{SolutionFile} was not found above {AppContext.BaseDirectory}.");
    }
}
