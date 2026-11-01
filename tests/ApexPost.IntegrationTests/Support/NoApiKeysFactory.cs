namespace ApexPost.IntegrationTests.Support;

/// <summary>Outside Development, so no user-secrets apply, with blank keys: nothing is configured.</summary>
public sealed class NoApiKeysFactory : ProductionFactory
{
    protected override IEnumerable<KeyValuePair<string, string?>> ExtraSettings =>
        [new("Integration:ApiKeys:Acme", string.Empty), new("Integration:ApiKeys:Burst", string.Empty)];
}
