using ApexPost.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ApexPost.UnitTests.Infrastructure;

/// <summary>The committed deployment script must keep up with the migrations it stands in for.</summary>
public sealed class MigrationScriptTests
{
    private const string SolutionFile = "Apex_Post_Logistic.slnx";
    private static readonly string ScriptPath = Path.Combine("sql", "migrations", "idempotent-migrations.sql");

    [Fact]
    public async Task The_idempotent_script_covers_every_migration()
    {
        var options = new DbContextOptionsBuilder<ApexPostDbContext>().UseSqlServer("Server=unused").Options;
        await using var database = new ApexPostDbContext(options);
        var migrations = database.Database.GetMigrations().ToList();

        var script = await File.ReadAllTextAsync(
            Path.Combine(SolutionFolder(), ScriptPath),
            TestContext.Current.CancellationToken);

        Assert.NotEmpty(migrations);
        Assert.All(migrations, migration => Assert.Contains($"N'{migration}'", script, StringComparison.Ordinal));
    }

    private static string SolutionFolder()
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (folder is not null && !File.Exists(Path.Combine(folder.FullName, SolutionFile)))
        {
            folder = folder.Parent;
        }

        return folder?.FullName ?? throw new InvalidOperationException($"{SolutionFile} was not found.");
    }
}
