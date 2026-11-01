using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ApexPost.Infrastructure.Persistence.Json;
using ApexPost.IntegrationTests.Support;
using Microsoft.Extensions.Logging;

namespace ApexPost.IntegrationTests.Middleware;

/// <summary>Outside Development. A malformed staff file makes the sign-in fail with an unexpected exception.</summary>
public sealed class ExceptionHandlingTests(ProductionFactory factory)
    : IClassFixture<ProductionFactory>, IAsyncLifetime
{
    private const string LoginPath = "/Account/Login";

    public async ValueTask InitializeAsync()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(factory.DataFile(DataFiles.Staff))!);
        await File.WriteAllTextAsync(
            factory.DataFile(DataFiles.Staff),
            "{ not json",
            TestContext.Current.CancellationToken);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Json_caller_gets_problem_details_without_internals()
    {
        using var browser = new CounterBrowser(factory);

        using var response = await FailSignInAsync(browser);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var cancellationToken = TestContext.Current.CancellationToken;
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        Assert.Equal(500, problem.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrEmpty(problem.GetProperty("traceId").GetString()));
        Assert.Equal(
            Assert.Single(response.Headers.GetValues("X-Correlation-Id")),
            problem.GetProperty("correlationId").GetString());
        var body = problem.GetRawText();
        Assert.False(problem.TryGetProperty("stackTrace", out _));
        Assert.DoesNotContain("staff.json", body, StringComparison.Ordinal);
        Assert.DoesNotContain("   at ", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Browser_gets_the_error_page_without_internals_and_with_security_headers()
    {
        using var browser = new CounterBrowser(factory);
        browser.AcceptHtml();

        using var response = await FailSignInAsync(browser);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.True(response.Headers.Contains("Content-Security-Policy"));
        var page = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("500 – Internal Server Error", page, StringComparison.Ordinal);
        Assert.Contains("Trace ID:", page, StringComparison.Ordinal);
        Assert.Contains("Correlation ID:", page, StringComparison.Ordinal);
        Assert.Contains("Back to the start", page, StringComparison.Ordinal);
        Assert.DoesNotContain("staff.json", page, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Failure_is_logged_as_an_error_once_with_its_correlation_id()
    {
        using var browser = new CounterBrowser(factory);

        using var response = await FailSignInAsync(browser);

        var correlation = $"CorrelationId={Assert.Single(response.Headers.GetValues("X-Correlation-Id"))}";
        var entry = await factory.Logs.WaitForAsync(log =>
            log.Level == LogLevel.Error && log.Scopes.Contains(correlation));
        Assert.Contains("failed with status 500", entry.Message, StringComparison.Ordinal);
        Assert.Single(factory.Logs.Entries, log => log.Level == LogLevel.Error && log.Scopes.Contains(correlation));
    }

    private static Task<HttpResponseMessage> FailSignInAsync(CounterBrowser browser) =>
        browser.PostAsync(LoginPath, LoginPath, CounterBrowser.Login("someone@apexpost.test", "a-password"));
}
