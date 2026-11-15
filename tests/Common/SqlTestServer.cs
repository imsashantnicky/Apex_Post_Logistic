using System.Globalization;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace ApexPost.Tests.Common;

/// <summary>
/// The one place that finds a SQL Server for the integration tests. It tries, in this order:
/// <list type="number">
/// <item>the environment variable <c>ConnectionStrings__ApexPostTestServer</c> (the CI container);</item>
/// <item>the user-secrets shared by the web and API projects (<c>ConnectionStrings:ApexPostTestServer</c>, then
/// <c>ConnectionStrings:ApexPostDb</c>, as written by setup-secrets.bat);</item>
/// <item>on Windows, SQL Server Express LocalDB <c>(localdb)\MSSQLLocalDB</c> if it answers;</item>
/// <item>otherwise the test is skipped with <see cref="Assert.Skip(string)"/> and the list of what was tried.</item>
/// </list>
/// Every fixture gets its own database name from <see cref="NewDatabase"/>, so the development database is never
/// touched. A configured server is used as it is: if it is down, the tests fail instead of hiding the problem.
/// When the variable <c>CI</c> is <c>true</c>, a missing server fails the test instead of skipping it, so CI always
/// reports 0 skipped.
/// </summary>
/// <remarks>
/// xUnit v3 reports a skip only when it is thrown while a test runs (test method or test class constructor). A skip
/// thrown from a class fixture constructor is reported as a failure, so fixtures never call
/// <see cref="SkipIfUnavailable"/> when they are built, only when a test first uses the database.
/// </remarks>
public static class SqlTestServer
{
    /// <summary>The connection-string name used by CI and by the tests' user-secret.</summary>
    public const string TestServerName = "ApexPostTestServer";

    /// <summary>The application's own connection-string name; only its server part is reused.</summary>
    public const string ApplicationDatabaseName = "ApexPostDb";

    private const string WebUserSecretsId = "8ee05317-5b65-4866-a83b-e9e701628c18";
    private const string LocalDbConnection =
        @"Server=(localdb)\MSSQLLocalDB;Integrated Security=True;TrustServerCertificate=True;Connect Timeout=30";

    private const string UnavailableServer = "Server=sql-server-not-available";

    private static readonly Lazy<Lookup> Found = new(Find, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>True when one of the sources gave a server.</summary>
    public static bool IsAvailable => Found.Value.ConnectionString is not null;

    /// <summary>Where the server came from, or why there is none.</summary>
    public static string Description => Found.Value.Description;

    /// <summary>
    /// Skips the running test when no server was found (fails it in CI). Call it from a test, a test class
    /// constructor, or code a test calls (such as a factory's <c>CreateHost</c>); never from a fixture constructor.
    /// </summary>
    public static void SkipIfUnavailable()
    {
        if (IsAvailable)
        {
            return;
        }

        if (string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("CI must not skip SQL tests. " + Description);
        }

        Assert.Skip(Description);
    }

    /// <summary>
    /// A connection string to a new, unique database <c>ApexPostTests_{area}_{guid}</c> on the found server. The
    /// database is not created here. Without a server it points to a placeholder that is never opened, because the
    /// test is skipped first (<see cref="SkipIfUnavailable"/>).
    /// </summary>
    public static string NewDatabase(string area) =>
        new SqlConnectionStringBuilder(Found.Value.ConnectionString ?? UnavailableServer)
        {
            InitialCatalog = string.Create(
                CultureInfo.InvariantCulture,
                $"ApexPostTests_{area}_{Guid.NewGuid():N}"),
        }.ConnectionString;

    private static Lookup Find()
    {
        var environment = new ConfigurationBuilder().AddEnvironmentVariables().Build();
        if (environment.GetConnectionString(TestServerName) is { Length: > 0 } fromEnvironment)
        {
            return new(fromEnvironment, $"environment variable ConnectionStrings__{TestServerName}");
        }

        var secrets = new ConfigurationBuilder().AddUserSecrets(WebUserSecretsId).Build();
        foreach (var name in new[] { TestServerName, ApplicationDatabaseName })
        {
            if (secrets.GetConnectionString(name) is { Length: > 0 } fromSecrets)
            {
                return new(fromSecrets, $"user-secret ConnectionStrings:{name}");
            }
        }

        var localDb = OperatingSystem.IsWindows() ? TryLocalDb() : "not tried (not Windows)";
        return localDb is null
            ? new(LocalDbConnection, @"LocalDB (localdb)\MSSQLLocalDB")
            : new(null, "SQL Server not available: no ConnectionStrings__" + TestServerName
                + " environment variable, no ConnectionStrings:" + TestServerName + " or :" + ApplicationDatabaseName
                + " user-secret, LocalDB " + localDb
                + ". Set the variable, run setup-secrets.bat, or install SQL Server Express LocalDB.");
    }

    /// <summary>Null when LocalDB answered, otherwise the reason it did not.</summary>
    private static string? TryLocalDb()
    {
        try
        {
            using var connection = new SqlConnection(LocalDbConnection);
            connection.Open();
            return null;
        }
        catch (SqlException error)
        {
            return "did not answer (" + error.Message + ")";
        }
    }

    private sealed record Lookup(string? ConnectionString, string Description);
}
