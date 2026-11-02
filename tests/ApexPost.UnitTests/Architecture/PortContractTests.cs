using System.Reflection;
using ApexPost.Application.Abstractions;

namespace ApexPost.UnitTests.Architecture;

/// <summary>
/// The rules of the Week 3 ports, read from the compiled code: every asynchronous port can be cancelled, no port hands
/// out a query, a repository only stages, and the code around the database never takes a synchronous path.
/// </summary>
public sealed class PortContractTests
{
    private const string Repositories = "ApexPost.Infrastructure.Repositories";
    private const string Persistence = "ApexPost.Infrastructure.Persistence";
    private const string FileStorage = "ApexPost.Infrastructure.Persistence.Json";
    private const string Migrations = "ApexPost.Infrastructure.Persistence.Migrations";
    private const string Commit = "DbContext.SaveChanges";

    private static readonly string[] SynchronousDatabaseCalls =
    [
        Commit,
        "DatabaseFacade.BeginTransaction",
        "DatabaseFacade.EnsureCreated",
        "DatabaseFacade.EnsureCreatedAsync",
        "DatabaseFacade.EnsureDeleted",
        "RelationalDatabaseFacadeExtensions.BeginTransaction",
        "RelationalDatabaseFacadeExtensions.ExecuteSql",
        "RelationalDatabaseFacadeExtensions.ExecuteSqlRaw",
        "RelationalDatabaseFacadeExtensions.Migrate",
        "RelationalQueryableExtensions.ExecuteDelete",
        "RelationalQueryableExtensions.ExecuteUpdate",
    ];

    private static IEnumerable<Type> Ports =>
        typeof(IUnitOfWork).Assembly.GetTypes().Where(type =>
            type is { IsInterface: true, Namespace: "ApexPost.Application.Abstractions" });

    private static IEnumerable<Type> DatabaseCode =>
        Assembly.Load("ApexPost.Infrastructure").GetTypes().Where(type =>
            InNamespace(type, Repositories)
            || (InNamespace(type, Persistence) && !InNamespace(type, FileStorage) && !InNamespace(type, Migrations)));

    [Fact]
    public void Every_asynchronous_port_method_takes_a_cancellation_token()
    {
        var missing =
            from port in Ports
            from method in port.GetMethods()
            where IsAsynchronous(method.ReturnType)
                && method.GetParameters().All(parameter => parameter.ParameterType != typeof(CancellationToken))
            select $"{port.Name}.{method.Name}";

        Assert.Empty(missing);
    }

    [Fact]
    public void No_port_hands_out_a_query()
    {
        var leaking =
            from port in Ports
            from method in port.GetMethods()
            from type in method.GetParameters().Select(parameter => parameter.ParameterType).Append(method.ReturnType)
            where Mentions(type, typeof(IQueryable))
            select $"{port.Name}.{method.Name}";

        Assert.Empty(leaking);
    }

    [Fact]
    public void A_repository_stages_and_only_the_unit_of_work_commits()
    {
        var committing = Assembly.Load("ApexPost.Infrastructure").GetTypes()
            .Where(type => type.Namespace == Repositories)
            .Where(type => !type.FullName!.StartsWith($"{Repositories}.UnitOfWork", StringComparison.Ordinal));

        Assert.DoesNotContain(CodeUsage.CallsIn(committing), call => call.StartsWith(Commit, StringComparison.Ordinal));
    }

    [Fact]
    public void The_scanner_sees_the_commit_of_the_unit_of_work()
    {
        var unitOfWork = Assembly.Load("ApexPost.Infrastructure").GetTypes()
            .Where(type => type.FullName!.StartsWith($"{Repositories}.UnitOfWork", StringComparison.Ordinal));

        Assert.Contains($"{Commit}Async", CodeUsage.CallsIn(unitOfWork));
    }

    [Fact]
    public void The_code_around_the_database_takes_no_synchronous_path()
    {
        Assert.NotEmpty(DatabaseCode);
        Assert.Empty(CodeUsage.CallsIn(DatabaseCode).Intersect(SynchronousDatabaseCalls));
    }

    private static bool InNamespace(Type type, string name) =>
        type.Namespace == name || type.Namespace?.StartsWith($"{name}.", StringComparison.Ordinal) == true;

    private static bool IsAsynchronous(Type type) =>
        type == typeof(Task)
        || type == typeof(ValueTask)
        || (type.IsGenericType
            && (type.GetGenericTypeDefinition() == typeof(Task<>)
                || type.GetGenericTypeDefinition() == typeof(ValueTask<>)));

    private static bool Mentions(Type type, Type searched) =>
        searched.IsAssignableFrom(type) || type.GetGenericArguments().Any(argument => Mentions(argument, searched));
}
