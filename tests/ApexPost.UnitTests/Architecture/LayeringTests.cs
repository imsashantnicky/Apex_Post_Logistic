using System.Reflection;

namespace ApexPost.UnitTests.Architecture;

public sealed class LayeringTests
{
    private const string ProductPrefix = "ApexPost.";
    private const string Domain = "ApexPost.Domain";
    private const string Application = "ApexPost.Application";
    private const string Infrastructure = "ApexPost.Infrastructure";
    private const string Web = "ApexPost.Web";
    private const string FileApi = "System.IO";
    private const string JsonApi = "System.Text.Json";

    [Fact]
    public void Domain_references_no_ApexPost_assembly()
    {
        Assert.Empty(ReferencedProductAssemblies(Domain));
    }

    [Theory]
    [InlineData(Infrastructure)]
    [InlineData(Web)]
    public void Application_does_not_reference_outer_layers(string outerLayer)
    {
        Assert.DoesNotContain(outerLayer, ReferencedProductAssemblies(Application));
    }

    [Fact]
    public void Infrastructure_does_not_reference_the_web_layer()
    {
        Assert.DoesNotContain(Web, ReferencedProductAssemblies(Infrastructure));
    }

    [Theory]
    [InlineData(Domain)]
    [InlineData(Application)]
    public void Inner_layers_do_not_reference_the_json_assembly(string innerLayer)
    {
        var references = Assembly.Load(innerLayer).GetReferencedAssemblies().Select(reference => reference.Name);

        Assert.DoesNotContain(JsonApi, references);
    }

    [Theory]
    [InlineData(Domain, FileApi)]
    [InlineData(Domain, JsonApi)]
    [InlineData(Application, FileApi)]
    [InlineData(Application, JsonApi)]
    public void Inner_layers_use_no_file_or_json_api(string innerLayer, string forbiddenNamespace)
    {
        Assert.Empty(TypesFrom(innerLayer, forbiddenNamespace));
    }

    [Theory]
    [InlineData(FileApi)]
    [InlineData(JsonApi)]
    public void Scanner_finds_the_apis_that_infrastructure_really_uses(string apiNamespace)
    {
        Assert.NotEmpty(TypesFrom(Infrastructure, apiNamespace));
    }

    [Theory]
    [InlineData(Domain)]
    [InlineData(Application)]
    [InlineData(Infrastructure)]
    public void Layer_never_blocks_a_thread(string layer)
    {
        var types = Assembly.Load(layer).GetTypes();

        Assert.Empty(CodeUsage.BlockingCallsIn(types));
        Assert.Empty(CodeUsage.AsyncVoidMethodsIn(types));
    }

    [Fact]
    public void Scanner_finds_blocking_calls_and_async_void()
    {
        Type[] sample = [typeof(BlockingSample)];

        Assert.Equal(4, CodeUsage.BlockingCallsIn(sample).Count());
        Assert.Single(CodeUsage.AsyncVoidMethodsIn(sample));
    }

    private static string[] TypesFrom(string assemblyName, string apiNamespace) =>
    [
        .. from type in Assembly.Load(assemblyName).GetTypes()
           from used in CodeUsage.TypesUsedBy(type)
           where used.Namespace?.StartsWith(apiNamespace, StringComparison.Ordinal) == true
           select $"{type.FullName} -> {used.FullName}",
    ];

    private static string[] ReferencedProductAssemblies(string assemblyName) =>
    [
        .. Assembly.Load(assemblyName)
            .GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .Where(name => name.StartsWith(ProductPrefix, StringComparison.Ordinal)),
    ];
}
