using ApexPost.Courier.ApiClient.Auth;
using ApexPost.Courier.ApiClient.Resilience;
using ApexPost.Courier.ApiClient.Sync;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ApexPost.Courier.ApiClient.Tests.Support;

/// <summary>
/// The registration of <c>MauiProgram</c> (typed client, auth handler, resilience pipeline) with a fake primary
/// handler behind the <see cref="NetworkGuardHandler"/>, a captured log and short times so every test runs in well
/// under a second. An optional <see cref="TimeProvider"/> (a <c>FakeTimeProvider</c>) drives Polly's timeouts and
/// delays, so a timeout test advances the clock instead of waiting.
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

    /// <summary>
    /// Production retry count and breaker values with zero retry delay. The timeouts are long on purpose: they
    /// only fire when a test advances a fake clock, never on the wall clock.
    /// </summary>
    public static readonly ResilienceSettings NoDelaySettings = new()
    {
        TotalTimeout = TimeSpan.FromMinutes(5),
        AttemptTimeout = TimeSpan.FromSeconds(8),
        RetryBaseDelay = TimeSpan.Zero,
        RetryMaxDelay = TimeSpan.FromMilliseconds(1),
    };

    private readonly ServiceProvider _provider;
    private readonly ScriptedHttpHandler? _scripted;

    public ResilientClient(ScriptedHttpHandler handler, ResilienceSettings? settings = null, TimeProvider? time = null)
        : this((HttpMessageHandler)handler, settings, time) => _scripted = handler;

    public ResilientClient(HttpMessageHandler primary, ResilienceSettings? settings = null, TimeProvider? time = null)
    {
        var services = new ServiceCollection();
        if (time is not null)
        {
            services.AddSingleton(time);
        }

        services.AddLogging(logging => logging.SetMinimumLevel(LogLevel.Trace).AddProvider(Logs));
        services.AddSingleton<IAccessTokenStore>(new InMemoryTokenStore(Token));
        services.AddTransient<AuthHeaderHandler>();
        services.AddHttpClient<ApexApiClient>(ApexApiClient.HttpClientName, client => client.BaseAddress = ClientFactory.BaseAddress)
            .AddHttpMessageHandler<AuthHeaderHandler>()
            .AddApexResilience(settings ?? FastSettings)
            .ConfigurePrimaryHttpMessageHandler(() => new NetworkGuardHandler(primary));
        _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true });
    }

    public ScriptedHttpHandler Handler =>
        _scripted ?? throw new InvalidOperationException("This client was built on a non-scripted handler.");

    public CapturingLoggerProvider Logs { get; } = new();

    public ApexApiClient Api => _provider.GetRequiredService<ApexApiClient>();

    public ICircuitStateProvider Circuit => _provider.GetRequiredService<ICircuitStateProvider>();

    public HttpShipmentUploader Uploader => new(_provider.GetRequiredService<IHttpClientFactory>());

    public IEnumerable<CapturedLog> ResilienceLogs =>
        Logs.Entries.Where(entry => entry.Category == ApexResilience.LogCategory);

    public ValueTask DisposeAsync() => _provider.DisposeAsync();
}
