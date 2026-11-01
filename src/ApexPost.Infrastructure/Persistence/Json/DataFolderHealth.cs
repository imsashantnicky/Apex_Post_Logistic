using System.Globalization;
using ApexPost.Application.Abstractions;

namespace ApexPost.Infrastructure.Persistence.Json;

/// <summary>Proves the data folder is writable by creating and removing a small file in it.</summary>
public sealed class DataFolderHealth(JsonFileStore store) : IDataStoreHealth
{
    private const string ProbePrefix = ".health-";
    private const string PlainGuidFormat = "N";

    public async Task<bool> CanWriteAsync(CancellationToken cancellationToken)
    {
        var name = ProbePrefix + Guid.NewGuid().ToString(PlainGuidFormat, CultureInfo.InvariantCulture);
        var path = Path.Combine(store.DataFolder, name);
        try
        {
            Directory.CreateDirectory(store.DataFolder);
            await File.WriteAllBytesAsync(path, [], cancellationToken).ConfigureAwait(false);
            File.Delete(path);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
