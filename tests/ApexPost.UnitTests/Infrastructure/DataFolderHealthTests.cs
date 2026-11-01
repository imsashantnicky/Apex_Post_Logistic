using ApexPost.Infrastructure.Persistence.Json;
using ApexPost.UnitTests.Support;
using Microsoft.Extensions.Options;

namespace ApexPost.UnitTests.Infrastructure;

public sealed class DataFolderHealthTests : IDisposable
{
    private readonly TempDataFolder _folder = new();

    public void Dispose() => _folder.Dispose();

    [Fact]
    public async Task Writable_folder_is_healthy_and_keeps_no_probe_file()
    {
        using var store = StoreAt(_folder.PathOf("data"));

        var healthy = await new DataFolderHealth(store).CanWriteAsync(TestContext.Current.CancellationToken);

        Assert.True(healthy);
        Assert.Empty(Directory.GetFiles(_folder.PathOf("data")));
    }

    [Fact]
    public async Task Folder_that_cannot_be_created_is_unhealthy()
    {
        var blocked = _folder.PathOf("a-file");
        await File.WriteAllTextAsync(blocked, "in the way", TestContext.Current.CancellationToken);
        using var store = StoreAt(blocked);

        var healthy = await new DataFolderHealth(store).CanWriteAsync(TestContext.Current.CancellationToken);

        Assert.False(healthy);
    }

    private static JsonFileStore StoreAt(string folder) =>
        new(Options.Create(new JsonStorageSettings { DataFolder = folder }));
}
