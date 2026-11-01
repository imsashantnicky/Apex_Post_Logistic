using System.Net;
using ApexPost.IntegrationTests.Support;

namespace ApexPost.IntegrationTests.Security;

public sealed class SensitiveDataTests(ApexFactory factory) : IClassFixture<ApexFactory>
{
    private const string DocumentNumber = "QK7654321";
    private const string StoredLastCharacters = "\"DocumentLast4\":\"4321\"";

    [Fact]
    public async Task Document_number_is_only_stored_masked_and_is_never_logged()
    {
        using var clerk = await CounterBrowser.SignInAsync(factory, DemoAccounts.Clerk);
        var counter = await Counter.OpenAsync(clerk);

        using var verified = await counter.VerifyAsync(DocumentNumber);

        Assert.Equal(HttpStatusCode.Redirect, verified.StatusCode);
        var checks = await AuditTrail.RawChecksAsync(factory);
        Assert.Contains(StoredLastCharacters, checks, StringComparison.Ordinal);
        Assert.DoesNotContain(DocumentNumber, checks, StringComparison.Ordinal);
        Assert.DoesNotContain(DocumentNumber, await AuditTrail.RawRowsAsync(factory), StringComparison.Ordinal);
        Assert.DoesNotContain(DocumentNumber, await counter.PageAsync(), StringComparison.Ordinal);
        AssertAbsentFromLogs(DocumentNumber);
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
        Assert.DoesNotContain(ApexFactory.PartnerKey, await AuditTrail.RawRowsAsync(factory), StringComparison.Ordinal);
    }

    private void AssertAbsentFromLogs(string secret)
    {
        var written = factory.Logs.Entries.SelectMany(entry => entry.Scopes.Append(entry.Message));

        Assert.DoesNotContain(written, text => text.Contains(secret, StringComparison.Ordinal));
    }
}
