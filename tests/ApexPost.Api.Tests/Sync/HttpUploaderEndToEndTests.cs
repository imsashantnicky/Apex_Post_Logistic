using System.Net;
using System.Net.Http.Json;
using ApexPost.Api.Security;
using ApexPost.Api.Tests.Support;
using ApexPost.Courier.ApiClient;
using ApexPost.Courier.ApiClient.Auth;
using ApexPost.Courier.ApiClient.Resilience;
using ApexPost.Courier.ApiClient.Sync;
using ApexPost.Courier.Domain.Enums;
using ApexPost.Courier.Domain.Ports;
using ApexPost.Courier.Domain.Scans;
using Microsoft.Extensions.DependencyInjection;

namespace ApexPost.Api.Tests.Sync;

/// <summary>
/// The courier app's <see cref="HttpShipmentUploader"/> with the same handler chain as the app (auth header,
/// resilience) against the real API host and its SQL Server database: the Part D sync end to end.
/// </summary>
public sealed class HttpUploaderEndToEndTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_batch_is_stored_and_sending_it_again_counts_as_duplicates()
    {
        const string trackingNumber = "APX-20000701";
        await CreateShipmentAsync(trackingNumber);
        await using var device = Device(TestTokens.Create([ApiRoles.Courier]));
        var scans = new[] { Scan(trackingNumber, ScanType.Pickup, 30), Scan(trackingNumber, ScanType.InTransit, 20) };

        var first = await device.Uploader.UploadBatchAsync(trackingNumber, scans, Ct);
        var again = await device.Uploader.UploadBatchAsync(trackingNumber, scans, Ct);

        Assert.Equal(BatchUploadOutcome.Completed, first.Outcome);
        Assert.Empty(first.Rejected);
        Assert.Equal(BatchUploadOutcome.Completed, again.Outcome);
        Assert.Empty(again.Rejected);
        var shipment = await factory.CreateClient()
            .GetFromJsonAsync<ShipmentStatusOnly>($"api/v2/shipments/{trackingNumber}", Ct);
        Assert.Equal("InTransit", shipment?.Status);
        var stored = await factory.CreateClient()
            .GetFromJsonAsync<List<object>>($"api/v2/shipments/{trackingNumber}/scans", Ct);
        Assert.Equal(2, stored?.Count);
    }

    [Fact]
    public async Task A_scan_the_api_refuses_comes_back_as_rejected_with_a_reason()
    {
        const string trackingNumber = "APX-20000702";
        await CreateShipmentAsync(trackingNumber);
        await using var device = Device(TestTokens.Create([ApiRoles.Courier]));
        var inTransit = Scan(trackingNumber, ScanType.InTransit, 30);
        await device.Uploader.UploadBatchAsync(trackingNumber, [inTransit], Ct);
        var backwards = Scan(trackingNumber, ScanType.Pickup, 10);

        var result = await device.Uploader.UploadBatchAsync(trackingNumber, [backwards], Ct);

        Assert.Equal(BatchUploadOutcome.Completed, result.Outcome);
        Assert.False(string.IsNullOrWhiteSpace(Assert.Single(result.Rejected, pair => pair.Key == backwards.Id).Value));
    }

    [Fact]
    public async Task Without_a_token_the_batch_needs_a_new_sign_in()
    {
        await using var device = Device(token: null);

        var result = await device.Uploader.UploadBatchAsync(
            "APX-20000001", [Scan("APX-20000001", ScanType.Pickup, 5)], Ct);

        Assert.Equal(BatchUploadOutcome.Unauthorized, result.Outcome);
    }

    [Fact]
    public async Task Scans_of_another_courier_are_refused_as_a_whole()
    {
        await using var device = Device(TestTokens.Create([ApiRoles.Courier], courierId: TestTokens.OtherCourier));

        var result = await device.Uploader.UploadBatchAsync(
            "APX-20000001", [Scan("APX-20000001", ScanType.Pickup, 5)], Ct);

        Assert.Equal(BatchUploadOutcome.Refused, result.Outcome);
    }

    private async Task CreateShipmentAsync(string trackingNumber)
    {
        var response = await factory.CreateClient().PostAsJsonAsync("api/v1/shipments", new
        {
            trackingNumber,
            recipientName = "Ava Example",
            recipientPhone = "+44 20 7946 0101",
            recipientAddress = "12 Quay Street",
            recipientCity = "Harbour Point",
            weightKg = 2.5m,
        }, Ct);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private CourierDevice Device(string? token)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IAccessTokenStore>(new FixedTokenStore(token));
        services.AddTransient<AuthHeaderHandler>();
        services.AddHttpClient<ApexApiClient>(
                ApexApiClient.HttpClientName,
                client => client.BaseAddress = new Uri("https://localhost/"))
            .AddHttpMessageHandler<AuthHeaderHandler>()
            .AddApexResilience(new ResilienceSettings { RetryBaseDelay = TimeSpan.FromMilliseconds(1) })
            .ConfigurePrimaryHttpMessageHandler(() => factory.Server.CreateHandler());
        return new CourierDevice(services.BuildServiceProvider());
    }

    private static ShipmentScan Scan(string trackingNumber, ScanType type, int minutesAgo) => ShipmentScan.Restore(
        ScanIds.New(),
        trackingNumber,
        type,
        TestTokens.Courier,
        "Central Hub",
        DateTimeOffset.UtcNow.AddMinutes(-minutesAgo),
        null,
        SyncStatus.Pending);

    private sealed record ShipmentStatusOnly(string Status);

    private sealed class CourierDevice(ServiceProvider provider) : IAsyncDisposable
    {
        public HttpShipmentUploader Uploader { get; } = new(provider.GetRequiredService<IHttpClientFactory>());

        public ValueTask DisposeAsync() => provider.DisposeAsync();
    }

    private sealed class FixedTokenStore(string? token) : IAccessTokenStore
    {
        public Task<string?> GetAccessTokenAsync(CancellationToken ct) => Task.FromResult(token);

        public Task SaveAccessTokenAsync(string token, CancellationToken ct) => Task.CompletedTask;

        public void Clear()
        {
        }
    }
}
