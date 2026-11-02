using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ApexPost.IntegrationTests.Support;

/// <summary>Switches every new connection to a database user before EF Core uses it.</summary>
internal sealed class ImpersonatingInterceptor(string statement) : DbConnectionInterceptor
{
    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        using var command = connection.CreateCommand();
        command.CommandText = statement;
        command.ExecuteNonQuery();
    }

    public override async Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = statement;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
