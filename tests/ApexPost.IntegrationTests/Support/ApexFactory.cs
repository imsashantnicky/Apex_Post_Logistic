using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ApexPost.IntegrationTests.Support;

/// <summary>
/// The real application with the demo data on a temp folder, fast password hashing, known partner keys, a short
/// courier timeout, captured logs and a remote address chosen per client.
/// </summary>
public class ApexFactory : WebApplicationFactory<Program>
{
    public const string DemoPassword = "Demo-password-for-tests";
    public const string PartnerKey = "partner-key-for-tests";
    public const string BurstPartnerKey = "burst-partner-key-for-tests";
    public const string PartnerOrigin = "https://tracking.partner.example";

    private readonly string _dataFolder = Path.Combine(Path.GetTempPath(), "apexpost-web-" + Guid.NewGuid());

    public CapturedLogs Logs { get; } = new();

    protected virtual string EnvironmentName => Environments.Development;

    protected virtual IEnumerable<KeyValuePair<string, string?>> ExtraSettings => [];

    public string DataFile(string fileName) => Path.Combine(_dataFolder, fileName);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(EnvironmentName);
        builder.ConfigureLogging(logging => logging.AddProvider(Logs));
        builder.ConfigureServices(services => services.AddSingleton<IStartupFilter, RemoteAddressStartupFilter>());
        builder.ConfigureAppConfiguration((_, configuration) => configuration
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Storage:DataFolder"] = _dataFolder,
                ["Seed:Enabled"] = "true",
                ["Seed:DemoPassword"] = DemoPassword,
                ["Passwords:Iterations"] = "1000",
                ["Courier:TimeoutMilliseconds"] = "300",
                ["Integration:ApiKeys:Acme"] = PartnerKey,
                ["Integration:ApiKeys:Burst"] = BurstPartnerKey,
            })
            .AddInMemoryCollection(ExtraSettings));
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
