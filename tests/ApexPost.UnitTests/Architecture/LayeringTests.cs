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

    private static string[] ReferencedProductAssemblies(string assemblyName) =>
    [
        .. Assembly.Load(assemblyName)
            .GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .Where(name => name.StartsWith(ProductPrefix, StringComparison.Ordinal)),
    ];
}
