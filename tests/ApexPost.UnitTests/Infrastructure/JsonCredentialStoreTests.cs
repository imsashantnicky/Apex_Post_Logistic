using ApexPost.Infrastructure.Persistence.Json;
using ApexPost.Infrastructure.Security;
using ApexPost.UnitTests.Support;
using static ApexPost.UnitTests.Support.CounterSteps;

namespace ApexPost.UnitTests.Infrastructure;

public sealed class JsonCredentialStoreTests : IDisposable
{
    private readonly TempDataFolder _folder = new();

    public void Dispose() => _folder.Dispose();

    [Fact]
    public async Task Unknown_staff_has_no_hash()
    {
        Assert.Null(await new JsonCredentialStore(_folder.OpenStore()).GetHashAsync(Guid.NewGuid(), Ct));
    }

    [Fact]
    public async Task Hash_survives_a_restart()
    {
        var staffId = Guid.NewGuid();
        await new JsonCredentialStore(_folder.OpenStore()).SetHashAsync(staffId, "stored-hash", Ct);

        Assert.Equal("stored-hash", await new JsonCredentialStore(_folder.OpenStore()).GetHashAsync(staffId, Ct));
    }

    [Fact]
    public async Task Setting_a_new_hash_replaces_the_old_one_and_raises_the_version()
    {
        var staffId = Guid.NewGuid();
        var store = new JsonCredentialStore(_folder.OpenStore());
        await store.SetHashAsync(staffId, "old-hash", Ct);

        await store.SetHashAsync(staffId, "new-hash", Ct);

        Assert.Equal("new-hash", await store.GetHashAsync(staffId, Ct));
        var json = await File.ReadAllTextAsync(_folder.PathOf(DataFiles.Credentials), Ct);
        Assert.DoesNotContain("old-hash", json, StringComparison.Ordinal);
        Assert.Contains("\"version\": 2", json, StringComparison.Ordinal);
    }
}
