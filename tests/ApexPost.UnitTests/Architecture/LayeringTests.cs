using System.Reflection;

namespace ApexPost.UnitTests.Architecture;

public sealed class LayeringTests
{
    private const string ProductPrefix = "ApexPost.";

    [Fact]
    public void Domain_references_no_ApexPost_assembly()
    {
        Assert.Empty(ReferencedProductAssemblies("ApexPost.Domain"));
    }

    [Theory]
    [InlineData("ApexPost.Infrastructure")]
    [InlineData("ApexPost.Web")]
    public void Application_does_not_reference_outer_layers(string outerLayer)
    {
        Assert.DoesNotContain(outerLayer, ReferencedProductAssemblies("ApexPost.Application"));
    }

    [Theory]
    [InlineData("ApexPost.Domain")]
    [InlineData("ApexPost.Application")]
    public void Inner_layers_know_nothing_about_json(string innerLayer)
    {
        var references = Assembly.Load(innerLayer).GetReferencedAssemblies().Select(reference => reference.Name);

        Assert.DoesNotContain("System.Text.Json", references);
    }

    private static string[] ReferencedProductAssemblies(string assemblyName) =>
    [
        .. Assembly.Load(assemblyName)
            .GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .Where(name => name.StartsWith(ProductPrefix, StringComparison.Ordinal)),
    ];
}
