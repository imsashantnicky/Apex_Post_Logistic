using ApexPost.Courier.ApiClient;
using ApexPost.Courier.ApiClient.Auth;
using ApexPost.Courier.ApiClient.Http;
using ApexPost.Courier.ApiClient.Resilience;
using ApexPost.Courier.ApiClient.Sync;
using ApexPost.Courier.Domain.Ports;
using ApexPost.Courier.Offline;
using ApexPost.Courier.Offline.Outbox;
using ApexPost.Courier.Offline.Scans;
using ApexPost.Courier.Security;
using ApexPost.Courier.Sync;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ApexPost.Courier;

/// <summary>Composition root of the courier app.</summary>
public static class MauiProgram
{
    private const string SettingsResource = "ApexPost.Courier.appsettings.json";
    private const string ListenerLogFile = "listener.log";

    /// <summary>Builds the app: configuration, typed API client, token store, offline scan store and sync.</summary>
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        AddEmbeddedSettings(builder.Configuration);
        AddApiClient(builder);
        AddOfflineStore(builder.Services);
        AddSync(builder.Services);

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }

    private static void AddEmbeddedSettings(ConfigurationManager configuration)
    {
        var stream = typeof(MauiProgram).Assembly.GetManifestResourceStream(SettingsResource)
            ?? throw new InvalidOperationException($"Embedded resource '{SettingsResource}' is missing.");
        configuration.AddJsonStream(stream);
    }

    private static void AddApiClient(MauiAppBuilder builder)
    {
        var baseAddress = ApiBaseAddress.Choose(
            builder.Configuration[ApiBaseAddress.AndroidKey],
            builder.Configuration[ApiBaseAddress.WindowsKey],
            OperatingSystem.IsAndroid());

        builder.Services.AddSingleton<IAccessTokenStore, SecureStorageTokenStore>();
        builder.Services.AddTransient<AuthHeaderHandler>();

        // IHttpClientFactory owns the handlers and their lifetime; the app never creates an HttpClient itself.
        // Resilience (RESILIENCE.md): total timeout 30 s → retry ×3 → circuit breaker → attempt timeout 8 s.
        var client = builder.Services
            .AddHttpClient<ApexApiClient>(ApexApiClient.HttpClientName, http => http.BaseAddress = baseAddress)
            .AddHttpMessageHandler<AuthHeaderHandler>()
            .AddApexResilience();

#if DEBUG
        client.ConfigurePrimaryHttpMessageHandler(DevCertificateBypass.CreateHandler);
#endif
    }

    private static void AddOfflineStore(IServiceCollection services)
    {
        services.AddSingleton(provider => new CourierDatabase(
            Path.Combine(FileSystem.Current.AppDataDirectory, CourierDatabase.DefaultFolderName),
            provider.GetRequiredService<ILogger<CourierDatabase>>()));
        services.AddSingleton<CouchbaseScanRepository>();
        services.AddSingleton<IScanRepository>(provider => provider.GetRequiredService<CouchbaseScanRepository>());
        services.AddSingleton<IScanQueries>(provider => provider.GetRequiredService<CouchbaseScanRepository>());

        // The Part A listener: live lines go to a file in the app folder (the working directory is not writable
        // on Android), not to the console; the pending count reaches the UI through PendingCountChanged.
        services.AddSingleton(provider => new ScanChangeMonitor(
            provider.GetRequiredService<CourierDatabase>(),
            provider.GetRequiredService<IScanQueries>(),
            TimeProvider.System,
            provider.GetRequiredService<ILogger<ScanChangeMonitor>>(),
            TextWriter.Null,
            Path.Combine(FileSystem.Current.AppDataDirectory, ListenerLogFile)));
    }

    // The HTTP uploader replaces the Part A fake here; the fake stays for the console harness and the tests.
    private static void AddSync(IServiceCollection services)
    {
        services.AddSingleton<HttpShipmentUploader>();
        services.AddSingleton<IShipmentUploader>(provider => provider.GetRequiredService<HttpShipmentUploader>());
        services.AddSingleton(provider => new OutboxService(
            provider.GetRequiredService<IScanQueries>(),
            provider.GetRequiredService<CouchbaseScanRepository>(),
            provider.GetRequiredService<HttpShipmentUploader>(),
            provider.GetRequiredService<ILogger<OutboxService>>()));
        services.AddSingleton(Connectivity.Current);
        services.AddSingleton<CourierSync>();
        services.AddSingleton<AppNavigator>();
        services.AddTransient<LoginPage>();
        services.AddTransient<MainPage>();
    }
}
