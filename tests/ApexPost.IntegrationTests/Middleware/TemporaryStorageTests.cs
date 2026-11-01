using ApexPost.Infrastructure.Persistence.Json;
using ApexPost.IntegrationTests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ApexPost.IntegrationTests.Middleware;

public sealed class TemporaryStorageTests
{
    [Fact]
    public void Every_factory_stores_its_data_in_its_own_temp_folder()
    {
        using var first = new ApexFactory();
        using var second = new ApexFactory();

        var folder = first.Services.GetRequiredService<IOptions<JsonStorageSettings>>().Value.DataFolder;
        _ = second.Services;

        Assert.Equal(first.DataFolder, folder);
        Assert.StartsWith(Path.GetTempPath(), folder, StringComparison.Ordinal);
        Assert.NotEqual(first.DataFolder, second.DataFolder);
        Assert.True(File.Exists(first.DataFile(DataFiles.Transactions)));
        Assert.True(File.Exists(second.DataFile(DataFiles.Transactions)));
    }
}
