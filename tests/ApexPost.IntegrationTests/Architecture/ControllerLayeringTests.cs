using ApexPost.UnitTests.Architecture;
using Microsoft.AspNetCore.Mvc;

namespace ApexPost.IntegrationTests.Architecture;

public sealed class ControllerLayeringTests
{
    private const string InfrastructureAssembly = "ApexPost.Infrastructure";
    private const string CompositionNamespace = "ApexPost.Web.Composition";

    private static readonly Type[] WebTypes = typeof(Program).Assembly.GetTypes();

    [Fact]
    public void No_controller_depends_on_an_infrastructure_type()
    {
        var controllerCode = WebTypes.Where(type => Outermost(type).IsAssignableTo(typeof(ControllerBase)));

        Assert.Empty(InfrastructureUseIn(controllerCode));
    }

    [Fact]
    public void Only_the_composition_root_touches_infrastructure()
    {
        var outsideTheRoot = WebTypes.Where(type => !IsCompositionRoot(type));

        Assert.Empty(InfrastructureUseIn(outsideTheRoot));
    }

    [Fact]
    public void Scanner_sees_the_infrastructure_use_of_the_composition_root()
    {
        Assert.NotEmpty(InfrastructureUseIn(WebTypes.Where(IsCompositionRoot)));
    }

    [Fact]
    public void Web_layer_never_blocks_a_thread()
    {
        Assert.Empty(CodeUsage.BlockingCallsIn(WebTypes));
        Assert.Empty(CodeUsage.AsyncVoidMethodsIn(WebTypes));
    }

    private static string[] InfrastructureUseIn(IEnumerable<Type> types) =>
    [
        .. from type in types
           from used in CodeUsage.TypesUsedBy(type)
           where used.Assembly.GetName().Name == InfrastructureAssembly
           select $"{type.FullName} -> {used.Name}",
    ];

    private static bool IsCompositionRoot(Type type)
    {
        var outer = Outermost(type);
        return outer == typeof(Program) || outer.Namespace == CompositionNamespace;
    }

    private static Type Outermost(Type type) => type.DeclaringType is { } outer ? Outermost(outer) : type;
}
