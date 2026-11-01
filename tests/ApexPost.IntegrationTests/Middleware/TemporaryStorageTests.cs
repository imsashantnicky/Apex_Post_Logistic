using ApexPost.Domain.Branches;
using ApexPost.IntegrationTests.Support;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ApexPost.IntegrationTests.Middleware;

public sealed class TemporaryStorageTests
{
    private const string ApplicationDatabase = "ApexPostDb";

    [Fact]
    public async Task Every_factory_stores_its_data_in_its_own_temporary_database()
    {
        await using var first = new ApexFactory();
        await using var second = new ApexFactory();
        _ = first.Services.GetRequiredService<IServiceScopeFactory>();
        _ = second.Services.GetRequiredService<IServiceScopeFactory>();

        var firstName = new SqlConnectionStringBuilder(first.ConnectionString).InitialCatalog;
        var secondName = new SqlConnectionStringBuilder(second.ConnectionString).InitialCatalog;

        Assert.NotEqual(ApplicationDatabase, firstName);
        Assert.NotEqual(firstName, secondName);
        await using var database = first.OpenDatabase();
        Assert.True(await database.Set<Branch>().AnyAsync(TestContext.Current.CancellationToken));
    }
}
