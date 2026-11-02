using ApexPost.Infrastructure.Persistence;
using ApexPost.IntegrationTests.Support;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ApexPost.IntegrationTests.Sql;

/// <summary>The tests must never reach the development database, and must leave no database behind.</summary>
public sealed class DisposableDatabaseTests(ApexFactory factory) : IClassFixture<ApexFactory>
{
    private const string TestDatabasePrefix = "ApexPostTests_";
    private const string DevelopmentDatabase = "ApexPostDb";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_host_works_on_a_temporary_database_and_not_on_the_development_one()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var contexts = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApexPostDbContext>>();
        await using var application = await contexts.CreateDbContextAsync(Ct);

        var database = application.Database.GetDbConnection().Database;

        Assert.StartsWith(TestDatabasePrefix, database, StringComparison.Ordinal);
        Assert.NotEqual(DevelopmentDatabase, database);
        Assert.Equal(new SqlConnectionStringBuilder(factory.ConnectionString).InitialCatalog, database);
    }

    [Fact]
    public async Task The_temporary_database_is_dropped_with_its_factory()
    {
        string connectionString;
        string database;
        using (var temporary = new ApexFactory())
        {
            await using (temporary.OpenDatabase())
            {
                connectionString = temporary.ConnectionString;
                database = new SqlConnectionStringBuilder(connectionString).InitialCatalog;
                Assert.True(await ExistsAsync(connectionString, database));
            }
        }

        Assert.False(await ExistsAsync(connectionString, database));
    }

    private static async Task<bool> ExistsAsync(string connectionString, string database)
    {
        var master = new SqlConnectionStringBuilder(connectionString) { InitialCatalog = "master" };
        await using var connection = new SqlConnection(master.ConnectionString);
        await connection.OpenAsync(Ct);
        await using var command = new SqlCommand("SELECT DB_ID(@name)", connection);
        command.Parameters.AddWithValue("@name", database);
        return await command.ExecuteScalarAsync(Ct) is not (null or DBNull);
    }
}
