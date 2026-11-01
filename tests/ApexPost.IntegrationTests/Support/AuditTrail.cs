using ApexPost.Application.Abstractions;
using ApexPost.Application.DTOs;
using ApexPost.Infrastructure.Persistence.Json;
using ApexPost.Web.Middleware;
using Microsoft.Extensions.DependencyInjection;

namespace ApexPost.IntegrationTests.Support;

/// <summary>Reads back what a request left in the audit log.</summary>
internal static class AuditTrail
{
    public static async Task<AuditEntry> EntryForAsync(ApexFactory factory, HttpResponseMessage response, string name)
    {
        var correlationId = Assert.Single(response.Headers.GetValues(ApexHeaders.CorrelationId));
        var entries = await EntriesAsync(factory);
        return Assert.Single(entries, entry => entry.CorrelationId == correlationId && entry.Event == name);
    }

    public static Task<string> RawFileAsync(ApexFactory factory) =>
        File.ReadAllTextAsync(factory.DataFile(DataFiles.Audit), TestContext.Current.CancellationToken);

    public static async Task<IReadOnlyList<AuditEntry>> EntriesAsync(ApexFactory factory)
    {
        var store = factory.Services.GetRequiredService<IAuditStore>();
        var page = new PageRequest(1, PageRequest.MaximumSize);
        return (await store.ReadAsync(page, TestContext.Current.CancellationToken)).Items;
    }
}
