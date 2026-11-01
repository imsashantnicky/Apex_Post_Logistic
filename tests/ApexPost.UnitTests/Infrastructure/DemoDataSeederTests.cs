using ApexPost.Application.Abstractions;
using ApexPost.Domain.Personnel;
using ApexPost.Domain.Transactions;
using ApexPost.Infrastructure.Persistence.Json;
using ApexPost.Infrastructure.Seeding;
using ApexPost.UnitTests.Support;
using Microsoft.Extensions.DependencyInjection;
using static ApexPost.UnitTests.Support.CounterSteps;

namespace ApexPost.UnitTests.Infrastructure;

public sealed class DemoDataSeederTests : IDisposable
{
    private readonly TempDataFolder _folder = new();

    public void Dispose() => _folder.Dispose();

    [Theory]
    [InlineData("Production", true)]
    [InlineData("Staging", true)]
    [InlineData("Development", false)]
    public async Task Nothing_is_seeded_outside_Development_or_when_disabled(string environment, bool enabled)
    {
        await using var host = Host(environment, enabled);

        var seeded = await host.GetRequiredService<DemoDataSeeder>().SeedIfAllowedAsync(Ct);

        Assert.False(seeded);
        Assert.Empty(Directory.GetFiles(_folder.Location));
    }

    [Fact]
    public async Task Production_without_a_demo_password_starts_normally()
    {
        await using var host = Host("Production", enabled: false, demoPassword: string.Empty);

        Assert.False(await host.GetRequiredService<DemoDataSeeder>().SeedIfAllowedAsync(Ct));
    }

    [Fact]
    public async Task Development_with_seeding_enabled_stores_all_demo_data()
    {
        await using var host = Host();

        Assert.True(await host.GetRequiredService<DemoDataSeeder>().SeedIfAllowedAsync(Ct));

        var targets = host.GetRequiredService<SeedTargets>();
        Assert.Equal(6, (await targets.Branches.ListAsync(Ct)).Count);
        Assert.Equal(26, (await targets.Staff.ListAsync(Ct)).Count);
        Assert.Equal(32, (await targets.Products.ListAsync(Ct)).Count);
        Assert.Equal(10, (await targets.Zones.ListAsync(Ct)).Count);
        Assert.Equal(6 * 32, (await targets.Stock.ListAsync(Ct)).Count);
    }

    [Fact]
    public async Task Stored_transactions_reload_through_the_domain_with_their_totals()
    {
        await using var host = Host();
        await host.GetRequiredService<DemoDataSeeder>().SeedIfAllowedAsync(Ct);

        var reloaded = await host.GetRequiredService<IRepository<CounterTransaction, Guid>>().ListAsync(Ct);

        Assert.Equal(48, reloaded.Count);
        Assert.Contains(reloaded, item => item.Lines.OfType<ShippingLineItem>().Any());
        Assert.Contains(reloaded, item => item.Lines.OfType<MoneyOrderLineItem>().Any());
        Assert.All(
            reloaded.Where(item => item.Status == TransactionStatus.Paid),
            item => Assert.True(item.Total.Amount > 0m));
        Assert.Contains(reloaded, item => item.Reversal is not null);
    }

    [Fact]
    public async Task Every_account_signs_in_with_the_demo_password_and_it_is_never_stored_in_clear()
    {
        await using var host = Host();
        await host.GetRequiredService<DemoDataSeeder>().SeedIfAllowedAsync(Ct);
        var hasher = host.GetRequiredService<IPasswordHasher>();
        var credentials = host.GetRequiredService<ICredentialStore>();

        foreach (var member in await host.GetRequiredService<IRepository<Staff, Guid>>().ListAsync(Ct))
        {
            var hash = await credentials.GetHashAsync(member.Id, Ct);
            Assert.True(hasher.Verify(InfrastructureHost.DemoPassword, hash!));
        }

        var json = await File.ReadAllTextAsync(_folder.PathOf(DataFiles.Credentials), Ct);
        Assert.DoesNotContain(InfrastructureHost.DemoPassword, json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Existing_data_is_never_seeded_again()
    {
        await using var host = Host();
        var seeder = host.GetRequiredService<DemoDataSeeder>();
        await seeder.SeedIfAllowedAsync(Ct);
        var before = await File.ReadAllTextAsync(_folder.PathOf(DataFiles.Transactions), Ct);

        Assert.False(await seeder.SeedIfAllowedAsync(Ct));

        Assert.Equal(before, await File.ReadAllTextAsync(_folder.PathOf(DataFiles.Transactions), Ct));
    }

    [Fact]
    public async Task Missing_demo_password_stops_seeding_before_anything_is_written()
    {
        await using var host = Host(demoPassword: " ");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            host.GetRequiredService<DemoDataSeeder>().SeedIfAllowedAsync(Ct));

        Assert.Empty(Directory.GetFiles(_folder.Location));
    }

    private ServiceProvider Host(
        string environment = "Development",
        bool enabled = true,
        string demoPassword = InfrastructureHost.DemoPassword) =>
        InfrastructureHost.Build(_folder.Location, _folder.Location, environment, enabled, demoPassword);
}
