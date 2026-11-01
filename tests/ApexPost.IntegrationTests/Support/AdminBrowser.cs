namespace ApexPost.IntegrationTests.Support;

/// <summary>Signs in and moves onto the admin allowlist, as an administrator at head office would be.</summary>
internal static class AdminBrowser
{
    public static async Task<CounterBrowser> SignInAsync(ApexFactory factory, string email)
    {
        var browser = await CounterBrowser.SignInAsync(factory, email);
        browser.CallFrom(PartnerClient.AllowedAddress);
        return browser;
    }
}
