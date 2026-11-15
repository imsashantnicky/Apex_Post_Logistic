using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace ApexPost.Infrastructure.Tracking;

/// <summary>
/// Used only by the dotnet-ef tool for the tracking migrations. It reads ConnectionStrings:ApexPostDb from the
/// environment or from the user-secrets of the web project, so no connection string is stored in the repository.
/// </summary>
public sealed class TrackingDbContextFactory : IDesignTimeDbContextFactory<TrackingDbContext>
{
    private const string WebUserSecretsId = "8ee05317-5b65-4866-a83b-e9e701628c18";

    public TrackingDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets(WebUserSecretsId)
            .AddEnvironmentVariables()
            .Build();
        var connectionString = configuration.GetConnectionString(TrackingDependencyInjection.ConnectionName)
            ?? throw new InvalidOperationException("Connection string 'ApexPostDb' is missing. Run setup-secrets.bat.");
        var options = new DbContextOptionsBuilder<TrackingDbContext>();
        TrackingDependencyInjection.UseTrackingSqlServer(options, connectionString);
        return new TrackingDbContext(options.Options);
    }
}
