using ApexPost.Courier.Domain.Scans;
using ApexPost.Courier.Offline;

namespace ApexPost.Courier.Offline.Tests.Architecture;

public sealed class DomainDependencyTests
{
    private const string CouchbasePrefix = "Couchbase";

    private static bool IsCouchbase(string? assemblyName) =>
        assemblyName?.StartsWith(CouchbasePrefix, StringComparison.OrdinalIgnoreCase) == true;

    [Fact]
    public void Courier_domain_references_no_Couchbase_assembly()
    {
        var references = typeof(ShipmentScan).Assembly.GetReferencedAssemblies();

        Assert.DoesNotContain(references, reference => IsCouchbase(reference.Name));
    }

    [Fact]
    public void Courier_domain_public_types_expose_no_Couchbase_type()
    {
        var exposed = typeof(ShipmentScan).Assembly.GetExportedTypes()
            .SelectMany(type => type.GetMembers())
            .SelectMany(member => member switch
            {
                System.Reflection.MethodInfo method =>
                    method.GetParameters().Select(p => p.ParameterType).Append(method.ReturnType),
                System.Reflection.PropertyInfo property => [property.PropertyType],
                _ => [],
            });

        Assert.DoesNotContain(exposed, type => IsCouchbase(type.Assembly.GetName().Name));
    }

    [Fact]
    public void Offline_project_is_the_one_that_references_Couchbase()
    {
        var references = typeof(CourierDatabase).Assembly.GetReferencedAssemblies();

        Assert.Contains(references, reference => IsCouchbase(reference.Name));
    }
}
