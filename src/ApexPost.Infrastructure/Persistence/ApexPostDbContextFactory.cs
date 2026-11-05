using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace ApexPost.Infrastructure.Persistence;

/// <summary>
/// Used only by the dotnet-ef tool. It reads ConnectionStrings:ApexPostDb from the environment or from the user-secrets
/// of the web project, so no connection string is ever stored in the repository.
/// </summary>
public sealed class ApexPostDbContextFactory : IDesignTimeDbContextFactory<ApexPostDbContext>
{
    private const string WebUserSecretsId = "8ee05317-5b65-4866-a83b-e9e701628c18";

    public ApexPostDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets(WebUserSecretsId)
            .AddEnvironmentVariables()
            .Build();
        var connectionString = configuration.GetConnectionString("ApexPostDb")
            ?? throw new InvalidOperationException("Connection string 'ApexPostDb' is missing. Run setup-secrets.bat.");
        var options = new DbContextOptionsBuilder<ApexPostDbContext>().UseSqlServer(connectionString).Options;
        return new ApexPostDbContext(options);
    }
}
