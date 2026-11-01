using ApexPost.Application.Abstractions;
using ApexPost.Domain.Branches;
using ApexPost.Infrastructure.Persistence.Json;
using ApexPost.UnitTests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace ApexPost.UnitTests.Infrastructure;

public sealed class DependencyInjectionTests : IDisposable
{
    private readonly TempDataFolder _folder = new();

    public void Dispose() => _folder.Dispose();

    [Fact]
    public void All_ports_resolve_with_scope_validation_on()
    {
        using var host = InfrastructureHost.Build(_folder.Location, _folder.Location);
        using var scope = host.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IRepository<Branch, BranchCode>>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ICredentialStore>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IAuditStore>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ITransactionQueries>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IReportQueries>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ICourierTrackingService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IIdentityVerificationService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<INotificationSink>());
    }

    [Fact]
    public void Products_and_zones_share_one_cache()
    {
        using var host = InfrastructureHost.Build(_folder.Location, _folder.Location);

        Assert.Same(host.GetRequiredService<IProductCatalog>(), host.GetRequiredService<IZoneCatalog>());
    }

    [Fact]
    public void Relative_data_folder_is_resolved_against_the_content_root()
    {
        using var host = InfrastructureHost.Build(_folder.Location, "data");

        Assert.Equal(_folder.PathOf("data"), host.GetRequiredService<JsonFileStore>().DataFolder);
    }
}
