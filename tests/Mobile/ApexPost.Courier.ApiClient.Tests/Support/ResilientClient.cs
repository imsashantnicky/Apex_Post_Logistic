using ApexPost.Courier.ApiClient.Auth;
using ApexPost.Courier.ApiClient.Resilience;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ApexPost.Courier.ApiClient.Tests.Support;

/// <summary>
/// The registration of <c>MauiProgram</c> (typed client, auth handler, resilience pipeline) with a scripted
/// primary handler, a captured log and short times so every test runs in well under a second.
/// </summary>
public sealed class ResilientClient : IAsyncDisposable
{
    public const string Token = "secret-test-token";

    public static readonly ResilienceSettings FastSettings = new()
    {
        TotalTimeout = TimeSpan.FromSeconds(5),
        AttemptTimeout = TimeSpan.FromMilliseconds(200),
        RetryBaseDelay = TimeSpan.FromMilliseconds(1),
        RetryMaxDelay = TimeSpan.FromMilliseconds(5),
        BreakDuration = TimeSpan.FromMilliseconds(500),
    };

    private readonly ServiceProvider _provider;

    public ResilientClient(ScriptedHttpHandler handler, ResilienceSettings? settings = null)
    {
        Handler = handler;
        var services = new ServiceCollection();
        services.AddLogging(logging => logging.SetMinimumLevel(LogLevel.Trace).AddProvider(Logs));
        services.AddSingleton<IAccessTokenStore>(new InMemoryTokenStore(Token));
        services.AddTransient<AuthHeaderHandler>();
        services.AddHttpClient<ApexApiClient>(client => client.BaseAddress = ClientFactory.BaseAddress)
            .AddHttpMessageHandler<AuthHeaderHandler>()
            .AddApexResilience(settings ?? FastSettings)
            .ConfigurePrimaryHttpMessageHandler(() => handler);
        _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true });
    }

    public ScriptedHttpHandler Handler { get; }

    public CapturingLoggerProvider Logs { get; } = new();

    public ApexApiClient Api => _provider.GetRequiredService<ApexApiClient>();

    public ICircuitStateProvider Circuit => _provider.GetRequiredService<ICircuitStateProvider>();

    public IEnumerable<CapturedLog> ResilienceLogs =>
        Logs.Entries.Where(entry => entry.Category == ApexResilience.LogCategory);

    public ValueTask DisposeAsync() => _provider.DisposeAsync();
}
