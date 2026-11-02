using Microsoft.Data.SqlClient;

namespace ApexPost.IntegrationTests.Support;

/// <summary>Reads and writes past EF Core, so that a test compares the application with what SQL itself says.</summary>
internal static class RawSql
{
    /// <summary>The single value of a query; a NULL comes back as the default of the type.</summary>
    public static async Task<T?> ScalarAsync<T>(this ApexFactory factory, string sql)
    {
        await using var connection = await OpenAsync(factory);
        await using var command = new SqlCommand(sql, connection);
        var value = await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);
        return value is null or DBNull ? default : (T)value;
    }

    /// <summary>Every row of the first column as text.</summary>
    public static async Task<List<string>> ColumnAsync(this ApexFactory factory, string sql)
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = await OpenAsync(factory);
        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(token);
        List<string> values = [];
        while (await reader.ReadAsync(token))
        {
            values.Add(reader.GetValue(0).ToString() ?? string.Empty);
        }

        return values;
    }

    public static async Task<int> ExecuteAsync(this ApexFactory factory, string sql)
    {
        await using var connection = await OpenAsync(factory);
        await using var command = new SqlCommand(sql, connection);
        return await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<SqlConnection> OpenAsync(ApexFactory factory)
    {
        await using (factory.OpenDatabase())
        {
            var connection = new SqlConnection(factory.ConnectionString);
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            return connection;
        }
    }
}
