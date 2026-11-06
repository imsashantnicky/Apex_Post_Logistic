using System.Net.Http.Headers;
using ApexPost.Client;
using ApexPost.Client.Consumer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

// Configuration: appsettings.json, then environment variables, then command-line arguments. The content root is the
// output folder, so appsettings.json is found from any working directory (dotnet run --project ...).
var builder = Host.CreateApplicationBuilder(
    new HostApplicationBuilderSettings { Args = args, ContentRootPath = AppContext.BaseDirectory });

builder.Services.AddOptions<ApexPostApiOptions>()
    .Bind(builder.Configuration.GetSection(ApexPostApiOptions.SectionName))
    .Validate(
        options => options.BaseUrl is { IsAbsoluteUri: true },
        $"{ApexPostApiOptions.SectionName}:BaseUrl must be an absolute URL such as http://localhost:5310/.")
    .ValidateOnStart();

// Typed client: IHttpClientFactory owns the HttpClient (pooled handlers); the generated class only receives it.
// The demo creates shipments and uploads CR-101 scans, so its token needs the Admin and Courier roles.
builder.Services.AddHttpClient<IApexPostApiClient, ApexPostApiClient>((services, http) =>
{
    var options = services.GetRequiredService<IOptions<ApexPostApiOptions>>().Value;
    http.BaseAddress = options.BaseAddress();
    if (!string.IsNullOrWhiteSpace(options.AccessToken))
    {
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", options.AccessToken);
    }
});

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton(Console.Out);
builder.Services.AddTransient<ShipmentDemo>();

using var host = builder.Build();
using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellation.Cancel();
};

var baseAddress = host.Services.GetRequiredService<IOptions<ApexPostApiOptions>>().Value.BaseAddress();
Console.WriteLine($"ApexPost API v2 at {baseAddress}");
try
{
    await host.Services.GetRequiredService<ShipmentDemo>().RunAsync(cancellation.Token);
    return 0;
}
catch (HttpRequestException exception)
{
    Console.Error.WriteLine($"The API at {baseAddress} cannot be reached ({exception.Message}). Is it running?");
    return 1;
}
catch (ApiException exception)
{
    // Status code only: the response body may echo request data, so it is not printed.
    Console.Error.WriteLine($"The API refused a request with status {exception.StatusCode}.");
    return 1;
}
