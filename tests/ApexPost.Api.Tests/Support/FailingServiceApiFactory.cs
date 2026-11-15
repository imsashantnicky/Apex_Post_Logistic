using ApexPost.Application.Tracking;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ApexPost.Api.Tests.Support;

/// <summary>
/// The API host in Production with <see cref="FailingTrackingService"/> in place of the real service, so the real
/// shipment endpoints throw. Production also means no seed (which would use the failing service) and no stack trace.
/// </summary>
public sealed class FailingServiceApiFactory : ApiFactory
{
    protected override string EnvironmentName => Environments.Production;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services =>
            services.AddScoped<IShipmentTrackingService, FailingTrackingService>());
    }
}
