using ApexPost.Application.Services;
using ApexPost.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ApexPost.UnitTests.Support;

/// <summary>The real infrastructure registrations on a temp folder, with fast password hashing.</summary>
internal static class InfrastructureHost
{
    public const string DemoPassword = "Demo-password-for-tests";

    public static ServiceProvider Build(
        string contentRoot,
        string dataFolder,
        string environment = "Development",
        bool seedEnabled = true,
        string demoPassword = DemoPassword)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Storage:DataFolder"] = dataFolder,
                ["Seed:Enabled"] = seedEnabled ? "true" : "false",
                ["Seed:DemoPassword"] = demoPassword,
                ["Passwords:Iterations"] = "1000",
            })
            .Build();
        var services = new ServiceCollection();
        services.AddSingleton<IHostEnvironment>(new FakeHostEnvironment(environment, contentRoot));
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddScoped<TaxCalculator>();
        services.AddJsonPersistence(configuration);
        services.AddInfrastructureServices(configuration);
        return services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }
}
