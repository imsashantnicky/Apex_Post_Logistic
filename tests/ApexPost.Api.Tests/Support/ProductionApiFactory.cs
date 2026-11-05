using Microsoft.Extensions.Hosting;

namespace ApexPost.Api.Tests.Support;

/// <summary>The API host in Production: no migrations, no seed, no stack traces.</summary>
public sealed class ProductionApiFactory : ApiFactory
{
    protected override string EnvironmentName => Environments.Production;
}
