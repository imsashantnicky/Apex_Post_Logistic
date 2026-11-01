using System.Net;
using ApexPost.Infrastructure.Persistence.Json;
using ApexPost.IntegrationTests.Support;

namespace ApexPost.IntegrationTests.Security;

public sealed class SensitiveDataTests(ApexFactory factory) : IClassFixture<ApexFactory>
{
    private const string DocumentNumber = "QK7654321";
    private const string MaskedNumber = "****4321";

    [Fact]
    public async Task Document_number_is_only_stored_masked_and_is_never_logged()
    {
        using var clerk = await CounterBrowser.SignInAsync(factory, DemoAccounts.Clerk);
        var counter = await Counter.OpenAsync(clerk);

        using var verified = await counter.VerifyAsync(DocumentNumber);

        Assert.Equal(HttpStatusCode.Redirect, verified.StatusCode);
        Assert.Contains(MaskedNumber, await ReadDataFileAsync(DataFiles.Transactions), StringComparison.Ordinal);
        AssertAbsentFromLogs(DocumentNumber);
        foreach (var file in Directory.GetFiles(factory.DataFolder))
        {
            Assert.DoesNotContain(DocumentNumber, await ReadDataFileAsync(file), StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Api_key_is_never_logged_or_audited()
    {
        using var partner = PartnerClient.Create(factory);

        using var response = await partner.GetAsync(
            new Uri(PartnerClient.Parcel, UriKind.Relative),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await factory.Logs.WaitForAsync(
            entry => entry.Message.Contains(PartnerClient.Parcel, StringComparison.Ordinal));
        AssertAbsentFromLogs(ApexFactory.PartnerKey);
        foreach (var file in Directory.GetFiles(factory.DataFolder))
        {
            Assert.DoesNotContain(ApexFactory.PartnerKey, await ReadDataFileAsync(file), StringComparison.Ordinal);
        }
    }

    private Task<string> ReadDataFileAsync(string file) =>
        File.ReadAllTextAsync(factory.DataFile(file), TestContext.Current.CancellationToken);

    private void AssertAbsentFromLogs(string secret)
    {
        var written = factory.Logs.Entries.SelectMany(entry => entry.Scopes.Append(entry.Message));

        Assert.DoesNotContain(written, text => text.Contains(secret, StringComparison.Ordinal));
    }
}
