using ApexPost.Api.Security;
using Microsoft.Extensions.Configuration;

namespace ApexPost.Api.Tests.Security;

/// <summary>The key lookup: exact match only, empty keys never match, no section means no partner.</summary>
public sealed class PartnerKeyStoreTests
{
    private static PartnerKeyStore StoreWith(params (string Partner, string? Key)[] keys) =>
        new(new ConfigurationBuilder()
            .AddInMemoryCollection(keys.Select(entry =>
                new KeyValuePair<string, string?>($"{PartnerKeyStore.SectionName}:{entry.Partner}", entry.Key)))
            .Build());

    [Fact]
    public void The_matching_key_names_its_partner()
    {
        var store = StoreWith(("Alpha", "alpha-key-0001"), ("Beta", "beta-key-0002"));

        Assert.Equal("Alpha", store.FindPartner("alpha-key-0001"));
        Assert.Equal("Beta", store.FindPartner("beta-key-0002"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("alpha-key-000")]
    [InlineData("alpha-key-00011")]
    [InlineData("ALPHA-KEY-0001")]
    [InlineData(" alpha-key-0001")]
    public void Anything_but_the_exact_key_matches_no_partner(string? presented)
    {
        var store = StoreWith(("Alpha", "alpha-key-0001"));

        Assert.Null(store.FindPartner(presented));
    }

    [Fact]
    public void A_partner_with_an_empty_key_is_disabled()
    {
        var store = StoreWith(("Disabled", string.Empty));

        Assert.Null(store.FindPartner(string.Empty));
        Assert.Null(store.FindPartner("anything"));
    }

    [Fact]
    public void Without_a_PartnerKeys_section_no_key_is_accepted()
    {
        var store = StoreWith();

        Assert.Null(store.FindPartner("alpha-key-0001"));
    }
}
