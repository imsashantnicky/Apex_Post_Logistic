using ApexPost.Application.Abstractions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace ApexPost.Infrastructure.Persistence;

/// <summary>Reports healthy while SQL Server answers and the database is not read-only.</summary>
public sealed class SqlDataStoreHealth(IDbContextFactory<ApexPostDbContext> contexts) : IDataStoreHealth
{
    private const string ReadWrite = "READ_WRITE";

    public async Task<bool> CanWriteAsync(CancellationToken cancellationToken)
    {
        try
        {
            var database = await contexts.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
            await using (database.ConfigureAwait(false))
            {
                var state = await database.Database
                    .SqlQuery<string>(
                        $"SELECT CONVERT(varchar(20), DATABASEPROPERTYEX(DB_NAME(), 'Updateability')) AS [Value]")
                    .SingleAsync(cancellationToken)
                    .ConfigureAwait(false);
                return state == ReadWrite;
            }
        }
        catch (Exception exception) when (exception is SqlException or InvalidOperationException)
        {
            return false;
        }
    }
}
