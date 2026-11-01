using ApexPost.Infrastructure.Persistence.Json;
using Microsoft.Extensions.Options;

namespace ApexPost.UnitTests.Support;

/// <summary>A throw-away data folder; each <see cref="OpenStore"/> acts like a restart of the application.</summary>
internal sealed class TempDataFolder : IDisposable
{
    private const string TempFilePattern = "*.tmp";

    private readonly List<JsonFileStore> _stores = [];

    public TempDataFolder() => Directory.CreateDirectory(Location);

    public string Location { get; } = Path.Combine(Path.GetTempPath(), "apexpost-tests", Guid.NewGuid().ToString());

    public JsonFileStore OpenStore()
    {
        var store = new JsonFileStore(Options.Create(new JsonStorageSettings { DataFolder = Location }));
        _stores.Add(store);
        return store;
    }

    public string PathOf(string fileName) => Path.Combine(Location, fileName);

    public string[] LeftoverTempFiles() => Directory.GetFiles(Location, TempFilePattern);

    public void Dispose()
    {
        _stores.ForEach(store => store.Dispose());
        Directory.Delete(Location, recursive: true);
    }
}
