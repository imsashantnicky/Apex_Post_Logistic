using ApexPost.Application.Abstractions;
using ApexPost.Application.Settings;
using ApexPost.Infrastructure.Persistence;
using ApexPost.Infrastructure.Persistence.Seeding;
using ApexPost.Infrastructure.Seeding;
using ApexPost.UnitTests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ApexPost.UnitTests.Infrastructure;

/// <summary>Each case stops at the gate: the database points nowhere, so reaching SQL Server would fail.</summary>
public sealed class DevelopmentSeederTests : IDisposable
{
    private const string NowhereConnection = "Server=127.0.0.1,1;Database=None;Connect Timeout=1";

    private readonly TempDataFolder _folder = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => _folder.Dispose();

    [Theory]
    [InlineData("Production", true, NowhereConnection)]
    [InlineData("Staging", true, NowhereConnection)]
    [InlineData("Development", false, NowhereConnection)]
    [InlineData("Development", true, "")]
    [InlineData("Development", true, null)]
    public async Task Seeder_writes_nothing_unless_development_enabled_and_connected(
        string environment,
        bool enabled,
        string? connection)
    {
        await using var host = InfrastructureHost.Build(_folder.Location, _folder.Location, environment, enabled);
        var settings = new Dictionary<string, string?> { ["ConnectionStrings:ApexPostDb"] = connection };
        await using var database = new ApexPostDbContext(
            new DbContextOptionsBuilder<ApexPostDbContext>().UseSqlServer(NowhereConnection).Options);
        var seeder = new DevelopmentSeeder(
            database,
            new FakeHostEnvironment(environment, _folder.Location),
            new ConfigurationBuilder().AddInMemoryCollection(settings).Build(),
            Options.Create(new SeedSettings { Enabled = enabled, DemoPassword = InfrastructureHost.DemoPassword }),
            host.GetRequiredService<SeedRules>(),
            host.GetRequiredService<IPasswordHasher>(),
            host.GetRequiredService<IClock>(),
            NullLogger<DevelopmentSeeder>.Instance);

        Assert.False(await seeder.SeedIfAllowedAsync(Ct));
    }
}
