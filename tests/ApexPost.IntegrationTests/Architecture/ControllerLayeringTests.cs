using System.Reflection;
using Microsoft.AspNetCore.Mvc;

namespace ApexPost.IntegrationTests.Architecture;

public sealed class ControllerLayeringTests
{
    private const string InfrastructureAssembly = "ApexPost.Infrastructure";

    private const BindingFlags DeclaredMembers =
        BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic |
        BindingFlags.DeclaredOnly;

    [Fact]
    public void No_controller_depends_on_an_infrastructure_type()
    {
        var violations = typeof(Program).Assembly
            .GetTypes()
            .Where(type => type.IsAssignableTo(typeof(ControllerBase)))
            .SelectMany(DependencyTypes, (controller, dependency) => (controller, dependency))
            .Where(pair => pair.dependency.Assembly.GetName().Name == InfrastructureAssembly)
            .Select(pair => $"{pair.controller.Name} -> {pair.dependency.Name}");

        Assert.Empty(violations);
    }

    private static IEnumerable<Type> DependencyTypes(Type controller) =>
        controller.GetConstructors(DeclaredMembers)
            .SelectMany(constructor => constructor.GetParameters())
            .Concat(controller.GetMethods(DeclaredMembers).SelectMany(method => method.GetParameters()))
            .Select(parameter => parameter.ParameterType)
            .Concat(controller.GetFields(DeclaredMembers).Select(field => field.FieldType))
            .Concat(controller.GetProperties(DeclaredMembers).Select(property => property.PropertyType));
}
