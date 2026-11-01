namespace ApexPost.IntegrationTests.Support;

/// <summary>Every action counts as slow.</summary>
public sealed class SlowActionFactory : ApexFactory
{
    protected override IEnumerable<KeyValuePair<string, string?>> ExtraSettings =>
        [new("Performance:SlowActionMs", "-1")];
}
