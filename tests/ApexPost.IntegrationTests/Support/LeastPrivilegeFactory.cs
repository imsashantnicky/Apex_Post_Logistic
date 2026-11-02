using ApexPost.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ApexPost.IntegrationTests.Support;

/// <summary>
/// The seeded test database with sql/security/least-privilege-role.sql applied, and a second server of the real
/// application whose every connection works as a member of that role. It impersonates a database user, because a
/// server may accept Windows logins only; the permissions checked are the same as those of a login in the role.
/// </summary>
public sealed class LeastPrivilegeFactory : ApexFactory
{
    private const string RestrictedUser = "apexpost_app_test";
    private const string Impersonate = $"EXECUTE AS USER = '{RestrictedUser}';";

    private bool _roleApplied;

    /// <summary>Runs against the role only; seeding stays with the owner, as in production.</summary>
    public WebApplicationFactory<Program> ApplicationServer()
    {
        ApplyRole();
        var restricted = new SqlConnectionStringBuilder(ConnectionString) { Pooling = false }.ConnectionString;
        return WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) => configuration
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Seed:Enabled"] = "false",
                    ["ConnectionStrings:ApexPostDb"] = restricted,
                }));
            builder.ConfigureTestServices(services => services.ConfigureDbContext<ApexPostDbContext>(options =>
                options.AddInterceptors(new ImpersonatingInterceptor(Impersonate))));
        });
    }

    public async Task<SqlConnection> OpenAsRestrictedUserAsync()
    {
        ApplyRole();
        var connection = new SqlConnection(new SqlConnectionStringBuilder(ConnectionString)
        {
            Pooling = false,
        }.ConnectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(Impersonate, connection);
        await command.ExecuteNonQueryAsync();
        return connection;
    }

    private void ApplyRole()
    {
        if (_roleApplied)
        {
            return;
        }

        _ = Services;
        using var connection = new SqlConnection(ConnectionString);
        connection.Open();
        foreach (var batch in RoleBatches().Append(
            $"CREATE USER {RestrictedUser} WITHOUT LOGIN; ALTER ROLE apexpost_app ADD MEMBER {RestrictedUser};"))
        {
            using var command = new SqlCommand(batch, connection);
            command.ExecuteNonQuery();
        }

        _roleApplied = true;
    }

    private static IEnumerable<string> RoleBatches()
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (folder is not null && folder.GetFiles("*.slnx").Length == 0)
        {
            folder = folder.Parent;
        }

        var script = Path.Combine(
            folder?.FullName ?? throw new InvalidOperationException("The solution folder was not found."),
            "sql",
            "security",
            "least-privilege-role.sql");
        return File.ReadAllText(script)
            .Split("\nGO", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(batch => batch.Length > 0);
    }
}
