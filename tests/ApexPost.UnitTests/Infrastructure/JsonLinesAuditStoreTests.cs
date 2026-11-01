using ApexPost.Application.DTOs;
using ApexPost.Domain.Branches;
using ApexPost.Domain.Personnel;
using ApexPost.Infrastructure.Auditing;
using ApexPost.Infrastructure.Persistence.Json;
using ApexPost.UnitTests.Support;
using static ApexPost.UnitTests.Support.CounterSteps;

namespace ApexPost.UnitTests.Infrastructure;

public sealed class JsonLinesAuditStoreTests : IDisposable
{
    private readonly TempDataFolder _folder = new();

    public void Dispose() => _folder.Dispose();

    [Fact]
    public async Task Entries_survive_a_restart_with_all_fields()
    {
        var entry = Entry("Sale");
        await new JsonLinesAuditStore(_folder.OpenStore()).AppendAsync(entry, Ct);

        var page = await new JsonLinesAuditStore(_folder.OpenStore()).ReadAsync(new PageRequest(), Ct);

        var read = Assert.Single(page.Items);
        Assert.Equal(entry.StaffId, read.StaffId);
        Assert.Equal(entry.Role, read.Role);
        Assert.Equal(entry.Branch, read.Branch);
        Assert.Equal("42", read.RouteValues["id"]);
        Assert.Equal(entry.CorrelationId, read.CorrelationId);
    }

    [Fact]
    public async Task Appending_keeps_earlier_lines_and_writes_one_line_per_entry()
    {
        var store = new JsonLinesAuditStore(_folder.OpenStore());
        await store.AppendAsync(Entry("First"), Ct);
        var before = await File.ReadAllTextAsync(_folder.PathOf(DataFiles.Audit), Ct);

        await store.AppendAsync(Entry("Second"), Ct);

        var after = await File.ReadAllTextAsync(_folder.PathOf(DataFiles.Audit), Ct);
        Assert.StartsWith(before, after, StringComparison.Ordinal);
        Assert.Equal(2, (await File.ReadAllLinesAsync(_folder.PathOf(DataFiles.Audit), Ct)).Length);
    }

    [Fact]
    public async Task Parallel_appends_keep_every_line_whole()
    {
        const int Writers = 30;
        var store = new JsonLinesAuditStore(_folder.OpenStore());

        await Task.WhenAll(Enumerable.Range(0, Writers).Select(_ => store.AppendAsync(Entry("Sale"), Ct)));

        Assert.Equal(Writers, (await store.ReadAsync(new PageRequest(size: PageRequest.MaximumSize), Ct)).TotalCount);
    }

    [Fact]
    public async Task Read_returns_the_newest_entries_first_in_pages()
    {
        var store = new JsonLinesAuditStore(_folder.OpenStore());
        foreach (var name in new[] { "First", "Second", "Third" })
        {
            await store.AppendAsync(Entry(name), Ct);
        }

        var page = await store.ReadAsync(new PageRequest(number: 1, size: 2), Ct);

        Assert.Equal(["Third", "Second"], page.Items.Select(entry => entry.Event));
        Assert.Equal(3, page.TotalCount);
    }

    [Fact]
    public async Task Missing_log_reads_as_empty()
    {
        var page = await new JsonLinesAuditStore(_folder.OpenStore()).ReadAsync(new PageRequest(), Ct);

        Assert.Empty(page.Items);
    }

    [Fact]
    public async Task Malformed_line_is_reported_with_its_number()
    {
        var store = new JsonLinesAuditStore(_folder.OpenStore());
        await store.AppendAsync(Entry("First"), Ct);
        await File.AppendAllTextAsync(_folder.PathOf(DataFiles.Audit), "{ broken\n", Ct);

        var exception = await Assert.ThrowsAsync<DataFileException>(() => store.ReadAsync(new PageRequest(), Ct));

        Assert.Contains("Line 2", exception.Message, StringComparison.Ordinal);
    }

    private static AuditEntry Entry(string name) =>
        new(
            TestData.Now,
            name,
            Guid.NewGuid(),
            StaffRole.CounterClerk,
            new BranchCode(TestData.HomeBranch),
            "Transactions",
            "Pay",
            new Dictionary<string, string> { ["id"] = "42" },
            302,
            12,
            "correlation-1");
}
