using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace ApexPost.Infrastructure.Persistence.Json;

/// <summary>
/// Reads and writes the JSON data files. Each file has its own SemaphoreSlim, so a read-change-write on one
/// file never interleaves with another call on the same file, while different files do not wait for each other.
/// A write goes to a temporary file first and then replaces the real file, so a crash or a cancelled write
/// never leaves a half-written file behind. Register as a singleton.
/// </summary>
public sealed class JsonFileStore : IDisposable
{
    private const int BufferSize = 4096;
    private const string TempSuffix = ".tmp";
    private const string PlainGuidFormat = "N";

    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new(StringComparer.Ordinal);

    public JsonFileStore(IOptions<JsonStorageSettings> settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        DataFolder = Path.GetFullPath(settings.Value.DataFolder);
    }

    public string DataFolder { get; }

    /// <summary>All records of a file; a file that does not exist yet has none.</summary>
    public async Task<List<TRecord>> ReadAsync<TRecord>(string fileName, CancellationToken cancellationToken)
    {
        var gate = LockFor(fileName);
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await ReadUnlockedAsync<TRecord>(fileName, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>
    /// Reads the records, lets <paramref name="change"/> edit the list and writes it back, all under the file's
    /// lock. If <paramref name="change"/> throws, the file is not touched.
    /// </summary>
    public async Task ChangeAsync<TRecord>(
        string fileName,
        Action<List<TRecord>> change,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(change);
        var gate = LockFor(fileName);
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var records = await ReadUnlockedAsync<TRecord>(fileName, cancellationToken).ConfigureAwait(false);
            change(records);
            await WriteUnlockedAsync(fileName, records, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>The same lock as <see cref="ChangeAsync{TRecord}"/>, for stores with their own file format.</summary>
    public async Task<T> WithLockAsync<T>(
        string fileName,
        Func<string, Task<T>> work,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(work);
        var gate = LockFor(fileName);
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await work(PathOf(fileName)).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    public void Dispose()
    {
        foreach (var gate in _locks.Values)
        {
            gate.Dispose();
        }

        _locks.Clear();
    }

    private async Task<List<TRecord>> ReadUnlockedAsync<TRecord>(string fileName, CancellationToken cancellationToken)
    {
        var path = PathOf(fileName);
        if (!File.Exists(path))
        {
            return [];
        }

        var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, useAsync: true);
        await using (stream.ConfigureAwait(false))
        {
            try
            {
                return await JsonSerializer
                        .DeserializeAsync<List<TRecord>>(stream, JsonDefaults.Files, cancellationToken)
                        .ConfigureAwait(false)
                    ?? throw new DataFileException($"Data file '{fileName}' holds null instead of a list.");
            }
            catch (JsonException exception)
            {
                throw new DataFileException($"Data file '{fileName}' is malformed: {exception.Message}", exception);
            }
        }
    }

    private async Task WriteUnlockedAsync<TRecord>(
        string fileName,
        List<TRecord> records,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(DataFolder);
        var path = PathOf(fileName);
        var tempPath = string.Concat(
            path,
            ".",
            Guid.NewGuid().ToString(PlainGuidFormat, CultureInfo.InvariantCulture),
            TempSuffix);
        try
        {
            var stream = new FileStream(
                tempPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                BufferSize,
                useAsync: true);
            await using (stream.ConfigureAwait(false))
            {
                await JsonSerializer.SerializeAsync(stream, records, JsonDefaults.Files, cancellationToken)
                    .ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            File.Move(tempPath, path, overwrite: true);
        }
        finally
        {
            File.Delete(tempPath);
        }
    }

    private SemaphoreSlim LockFor(string fileName) =>
        _locks.GetOrAdd(PathOf(fileName), static _ => new SemaphoreSlim(1, 1));

    private string PathOf(string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        return Path.Combine(DataFolder, Path.GetFileName(fileName));
    }
}
