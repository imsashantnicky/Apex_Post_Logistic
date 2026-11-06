using ApexPost.Courier.ApiClient;
using ApexPost.Courier.ApiClient.Auth;
using ApexPost.Courier.ApiClient.Http;
using ApexPost.Courier.Domain.Ports;
using ApexPost.Courier.Offline;
using ApexPost.Courier.Offline.Scans;
using ApexPost.Courier.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ApexPost.Courier;

/// <summary>Composition root of the courier app.</summary>
public static class MauiProgram
{
    private const string SettingsResource = "ApexPost.Courier.appsettings.json";

    /// <summary>Builds the app: configuration, typed API client, token store and the offline scan store.</summary>
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
        builder.Services.AddTransient<MainPage>();

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
        var client = builder.Services
            .AddHttpClient<ApexApiClient>(http => http.BaseAddress = baseAddress)
            .AddHttpMessageHandler<AuthHeaderHandler>();

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
    }
}
