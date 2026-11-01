using Microsoft.Extensions.Hosting;

namespace ApexPost.IntegrationTests.Support;

/// <summary>The application outside Development: no demo data, no stack traces, HSTS on.</summary>
public class ProductionFactory : ApexFactory
{
    public const string HttpsPort = "8443";

    protected override string EnvironmentName => Environments.Production;

    protected override IEnumerable<KeyValuePair<string, string?>> ExtraSettings =>
        [new("https_port", HttpsPort)];
}
