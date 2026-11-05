using ApexPost.Application.Abstractions;
using ApexPost.Application.Settings;
using ApexPost.Domain.Branches;
using ApexPost.Domain.Catalogue;
using ApexPost.Domain.Common;
using ApexPost.Domain.Inventory;
using ApexPost.Domain.Personnel;
using ApexPost.Domain.Shipping;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ApexPost.Infrastructure.Seeding;

/// <summary>
/// Writes the demo data through the repository ports, but only when the environment is Development AND
/// Seed:Enabled is true, and only into an empty store. All demo accounts share the password from user-secrets
/// (Seed:DemoPassword); it is hashed once, because PBKDF2 is deliberately slow.
/// </summary>
public sealed partial class DemoDataSeeder(
    IHostEnvironment environment,
    IOptions<SeedSettings> seed,
    SeedRules rules,
    SeedTargets targets,
    IClock clock,
    ILogger<DemoDataSeeder> logger)
{
    /// <summary>True when demo data was written; false when seeding is not allowed or data already exists.</summary>
    public async Task<bool> SeedIfAllowedAsync(CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment() || !seed.Value.Enabled)
        {
            LogSeedingSkipped(logger, environment.EnvironmentName);
            return false;
        }

        if ((await targets.Branches.ListAsync(cancellationToken).ConfigureAwait(false)).Count > 0)
        {
            return false;
        }

        var password = string.IsNullOrWhiteSpace(seed.Value.DemoPassword)
            ? throw new InvalidOperationException(
                "Seed:DemoPassword is not set. Run setup-secrets.bat or set it with dotnet user-secrets.")
            : seed.Value.DemoPassword;
        var data = DemoDataSet.Create(clock.UtcNow, rules.Payment, rules.Multipliers);
        await SaveAsync(data, targets.Hasher.Hash(password), cancellationToken).ConfigureAwait(false);
        LogSeeded(logger, data.Branches.Count, data.Staff.Count, data.Products.Count, data.Transactions.Count);
        return true;
    }

    private async Task SaveAsync(DemoDataSet data, string passwordHash, CancellationToken cancellationToken)
    {
        await AddAllAsync(targets.Branches, data.Branches, cancellationToken).ConfigureAwait(false);
        await AddAllAsync(targets.Zones, data.Zones, cancellationToken).ConfigureAwait(false);
        await AddAllAsync(targets.Products, data.Products, cancellationToken).ConfigureAwait(false);
        await AddAllAsync(targets.Stock, data.Stock, cancellationToken).ConfigureAwait(false);
        await AddAllAsync(targets.Staff, data.Staff, cancellationToken).ConfigureAwait(false);
        foreach (var member in data.Staff)
        {
            await targets.Credentials.SetHashAsync(member.Id, passwordHash, cancellationToken).ConfigureAwait(false);
        }

        await AddAllAsync(targets.Transactions, data.Transactions, cancellationToken).ConfigureAwait(false);
    }

    private static async Task AddAllAsync<TEntity, TId>(
        IRepository<TEntity, TId> repository,
        IEnumerable<TEntity> entities,
        CancellationToken cancellationToken)
        where TEntity : Entity<TId>
        where TId : notnull
    {
        foreach (var entity in entities)
        {
            await repository.AddAsync(entity, cancellationToken).ConfigureAwait(false);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Demo data not seeded (environment {Environment}).")]
    private static partial void LogSeedingSkipped(ILogger logger, string environment);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Seeded demo data: {Branches} branches, {Staff} staff, {Products} products, "
            + "{Transactions} transactions.")]
    private static partial void LogSeeded(ILogger logger, int branches, int staff, int products, int transactions);
}
