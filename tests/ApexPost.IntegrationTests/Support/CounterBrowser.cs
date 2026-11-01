using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ApexPost.IntegrationTests.Support;

/// <summary>A test browser: keeps cookies, does not follow redirects and posts with antiforgery tokens.</summary>
internal sealed partial class CounterBrowser : IDisposable
{
    private const string LoginPath = "/Account/Login";
    private const string TokenField = "__RequestVerificationToken";

    private readonly HttpClient _http;

    public CounterBrowser(ApexFactory factory)
    {
        _http = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
        });
    }

    public static async Task<CounterBrowser> SignInAsync(ApexFactory factory, string email)
    {
        var browser = new CounterBrowser(factory);
        using var response = await browser.PostAsync(LoginPath, LoginPath, Login(email, ApexFactory.DemoPassword));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return browser;
    }

    public static Dictionary<string, string> Login(string email, string password) =>
        new() { ["Email"] = email, ["Password"] = password };

    public Task<HttpResponseMessage> GetAsync(string path) => _http.GetAsync(new Uri(path, UriKind.Relative));

    public async Task<string> ReadPageAsync(string path)
    {
        using var response = await GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    /// <summary>Opens the page holding the form for its antiforgery token, then posts the form.</summary>
    public async Task<HttpResponseMessage> PostAsync(
        string pagePath,
        string formPath,
        IReadOnlyDictionary<string, string>? fields = null)
    {
        var page = await ReadPageAsync(pagePath);
        var token = Token().Match(page).Groups[1].Value;
        return await PostWithoutPageAsync(formPath, new(fields ?? new Dictionary<string, string>())
        {
            [TokenField] = token,
        });
    }

    public async Task<HttpResponseMessage> PostWithoutPageAsync(string formPath, Dictionary<string, string> fields)
    {
        using var content = new FormUrlEncodedContent(fields);
        return await _http.PostAsync(new Uri(formPath, UriKind.Relative), content);
    }

    public void Dispose() => _http.Dispose();

    [GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"")]
    private static partial Regex Token();
}
