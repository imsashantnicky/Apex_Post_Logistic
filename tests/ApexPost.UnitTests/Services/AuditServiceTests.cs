using ApexPost.Application.DTOs;
using ApexPost.Application.Exceptions;
using ApexPost.Application.Services;
using ApexPost.Domain.Personnel;
using ApexPost.Infrastructure.Auditing;
using ApexPost.UnitTests.Support;
using static ApexPost.UnitTests.Support.CounterSteps;

namespace ApexPost.UnitTests.Services;

public sealed class AuditServiceTests : IDisposable
{
    private readonly TempDataFolder _folder = new();

    public void Dispose() => _folder.Dispose();

    [Theory]
    [InlineData(StaffRole.CounterClerk)]
    [InlineData(StaffRole.BranchManager)]
    public async Task Staff_without_audit_access_are_refused(StaffRole role)
    {
        var service = ServiceFor(role);

        await Assert.ThrowsAsync<ForbiddenException>(() => service.ReadAsync(new PageRequest(), Ct));
        await Assert.ThrowsAsync<ForbiddenException>(() => service.ExportAsync(Ct));
    }

    [Theory]
    [InlineData(StaffRole.Auditor)]
    [InlineData(StaffRole.Administrator)]
    public async Task Export_returns_every_entry_across_pages(StaffRole role)
    {
        const int Count = PageRequest.MaximumSize + 5;
        var store = new JsonLinesAuditStore(_folder.OpenStore());
        for (var number = 0; number < Count; number++)
        {
            await store.AppendAsync(Entry(number), Ct);
        }

        var entries = await ServiceFor(role).ExportAsync(Ct);

        Assert.Equal(Count, entries.Count);
        Assert.Equal($"Event{Count - 1}", entries[0].Event);
    }

    private AuditService ServiceFor(StaffRole role) =>
        new(new JsonLinesAuditStore(_folder.OpenStore()), new FakeCurrentUser { Role = role });

    private static AuditEntry Entry(int number) => new(
        DateTimeOffset.UnixEpoch,
        $"Event{number}",
        null,
        null,
        null,
        "Controller",
        "Action",
        new Dictionary<string, string>(),
        200,
        1,
        "correlation");
}
