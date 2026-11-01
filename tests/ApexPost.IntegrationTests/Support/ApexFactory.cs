using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace ApexPost.IntegrationTests.Support;

/// <summary>The real application with the demo data on a temp folder and fast password hashing.</summary>
public sealed class ApexFactory : WebApplicationFactory<Program>
{
    public const string DemoPassword = "Demo-password-for-tests";

    private readonly string _dataFolder = Path.Combine(Path.GetTempPath(), "apexpost-web-" + Guid.NewGuid());

    public string DataFile(string fileName) => Path.Combine(_dataFolder, fileName);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(Environments.Development);
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Storage:DataFolder"] = _dataFolder,
                ["Seed:Enabled"] = "true",
                ["Seed:DemoPassword"] = DemoPassword,
                ["Passwords:Iterations"] = "1000",
            }));
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && Directory.Exists(_dataFolder))
        {
            Directory.Delete(_dataFolder, recursive: true);
        }
    }
}
