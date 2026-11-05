using System.Data;
using ApexPost.Infrastructure.Persistence;
using ApexPost.IntegrationTests.Support;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace ApexPost.IntegrationTests.Sql;

/// <summary>
/// Two real sessions on SQL Server: stock rows locked in opposite order deadlock with error 1205, which the
/// application's execution strategy retries; the same rows in one order only wait. Both transactions roll back.
/// </summary>
public sealed class DeadlockTests(ApexFactory factory) : IClassFixture<ApexFactory>
{
    private const int DeadlockVictim = 1205;
    private const string FirstSku = "PKG-0004";
    private const string SecondSku = "PKG-0005";

    // varchar like the column: an nvarchar parameter would convert the column and scan into the other session's row.
    private const int SkuLength = 8;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Opposite_order_deadlocks_with_an_error_the_strategy_retries()
    {
        await using var a = await SessionAsync();
        await using var b = await SessionAsync();
        await a.TakeAsync(FirstSku);
        await b.TakeAsync(SecondSku);

        var aWaits = a.TakeAsync(SecondSku);
        var bWaits = b.TakeAsync(FirstSku);
        var victim = await Assert.ThrowsAsync<SqlException>(() => Task.WhenAll(aWaits, bWaits));

        Assert.Equal(DeadlockVictim, victim.Number);
        using var scope = factory.Services.CreateScope();
        var strategy = new RetryProbe(scope.ServiceProvider.GetRequiredService<ApexPostDbContext>());
        Assert.True(strategy.Retries(victim));
    }

    [Fact]
    public async Task One_order_makes_the_second_session_wait_instead()
    {
        await using var a = await SessionAsync();
        await using var b = await SessionAsync();
        await a.TakeAsync(FirstSku);

        var bWaits = b.TakeAsync(FirstSku);
        await a.TakeAsync(SecondSku);
        Assert.False(bWaits.IsCompleted);
        await a.DisposeAsync();
        await bWaits;
        await b.TakeAsync(SecondSku);
    }

    [Fact]
    public void The_application_retries_transient_failures()
    {
        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<ApexPostDbContext>().Database;

        Assert.IsType<SqlServerRetryingExecutionStrategy>(database.CreateExecutionStrategy());
    }

    private async Task<Session> SessionAsync()
    {
        await factory.ScalarAsync<int>("SELECT 1");
        var connection = new SqlConnection(factory.ConnectionString);
        await connection.OpenAsync(Ct);
        return new Session(connection, (SqlTransaction)await connection.BeginTransactionAsync(Ct));
    }

    /// <summary>The strategy EnableRetryOnFailure installs, asked whether it would retry an error.</summary>
    private sealed class RetryProbe(DbContext context) : SqlServerRetryingExecutionStrategy(context)
    {
        public bool Retries(Exception exception) => ShouldRetryOn(exception);
    }

    /// <summary>One connection with an open transaction; disposing it rolls back and frees its locks.</summary>
    private sealed class Session(SqlConnection connection, SqlTransaction transaction) : IAsyncDisposable
    {
        private bool _disposed;

        public async Task TakeAsync(string sku)
        {
            await using var command = new SqlCommand(
                "UPDATE dbo.BranchStock SET Quantity = Quantity + 1 WHERE BranchCode = 'BR001' AND Sku = @Sku",
                connection,
                transaction);
            command.Parameters.Add("@Sku", SqlDbType.VarChar, SkuLength).Value = sku;
            await command.ExecuteNonQueryAsync(Ct);
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            await transaction.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
