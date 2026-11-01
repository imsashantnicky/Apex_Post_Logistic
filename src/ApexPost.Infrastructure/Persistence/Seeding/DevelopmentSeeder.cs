using ApexPost.Application.Abstractions;
using ApexPost.Application.Settings;
using ApexPost.Domain.Branches;
using ApexPost.Domain.Transactions;
using ApexPost.Infrastructure.Persistence.Configurations;
using ApexPost.Infrastructure.Persistence.Mappers;
using ApexPost.Infrastructure.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ApexPost.Infrastructure.Persistence.Seeding;

/// <summary>
/// Fills the SQL Server database with the Week 2 demo values and a larger history, but only in Development with
/// Seed:Enabled and a connection string, and only while Branches is empty. It never runs migrations. Everything is
/// written by one SaveChanges, so a failed run leaves the database empty and the next start tries again.
/// </summary>
public sealed partial class DevelopmentSeeder(
    ApexPostDbContext database,
    IHostEnvironment environment,
    IConfiguration configuration,
    IOptions<SeedSettings> seed,
    SeedRules rules,
    IPasswordHasher hasher,
    IClock clock,
    ILogger<DevelopmentSeeder> logger)
{
    /// <summary>True when demo data was written; false when seeding is not allowed or data already exists.</summary>
    public async Task<bool> SeedIfAllowedAsync(CancellationToken cancellationToken)
    {
        if (!IsAllowed())
        {
            LogSeedingSkipped(logger, environment.EnvironmentName);
            return false;
        }

        if (await database.Set<Branch>().AnyAsync(cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        var password = string.IsNullOrWhiteSpace(seed.Value.DemoPassword)
            ? throw new InvalidOperationException(
                "Seed:DemoPassword is not set. Run setup-secrets.bat or set it with dotnet user-secrets.")
            : seed.Value.DemoPassword;
        var transactions = Stage(hasher.Hash(password));
        await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        LogSeeded(logger, transactions);
        return true;
    }

    private bool IsAllowed() =>
        environment.IsDevelopment()
        && seed.Value.Enabled
        && !string.IsNullOrWhiteSpace(configuration.GetConnectionString(DependencyInjection.SqlConnectionName));

    private int Stage(string passwordHash)
    {
        var branches = DemoBranches.Create();
        var staff = DemoStaff.Create();
        var products = DemoProducts.Create();
        var zones = DemoZones.Create();
        var stock = DemoStock.Create(branches, products);
        var history = new DevelopmentHistory(branches, staff, products, zones, stock, rules.Payment, rules.Multipliers)
            .Create(clock.UtcNow);

        database.AddRange(branches);
        database.AddRange(products);
        database.AddRange(zones);
        database.AddRange(stock);
        database.AddRange(staff);
        foreach (var member in staff)
        {
            database.Entry(member).Property(StaffConfiguration.PasswordHash).CurrentValue = passwordHash;
        }

        database.AddRange(history);
        database.AddRange(history.SelectMany(Verifications));
        return history.Count;
    }

    private static IEnumerable<object> Verifications(CounterTransaction transaction) =>
        transaction.Verifications.Select(check => IdentityVerificationMapper.ToRecord(transaction.Id, check));

    [LoggerMessage(Level = LogLevel.Information, Message = "SQL demo data not seeded (environment {Environment}).")]
    private static partial void LogSeedingSkipped(ILogger logger, string environment);

    [LoggerMessage(Level = LogLevel.Information, Message = "Seeded the SQL database with {Transactions} transactions.")]
    private static partial void LogSeeded(ILogger logger, int transactions);
}
