using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using ApexPost.Courier.ApiClient.Contracts;
using ApexPost.Courier.ApiClient.Json;
using ApexPost.Courier.ApiClient.Resilience;
using ApexPost.Courier.ApiClient.Results;
using ApexPost.Courier.Domain.Scans;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace ApexPost.Courier.ApiClient;

/// <summary>
/// Typed client for the v2 courier endpoints. It is registered with <c>AddHttpClient&lt;ApexApiClient&gt;()</c>,
/// so the factory owns the <see cref="HttpClient"/>, its base address, the auth handler and the resilience
/// pipeline. Every method returns an <see cref="ApiResult{T}"/>: HTTP errors, bad JSON, network faults, timeouts
/// and an open circuit become typed failures, and only a cancellation requested by the caller is thrown.
/// </summary>
/// <param name="httpClient">Client with <see cref="HttpClient.BaseAddress"/> set to the API root.</param>
public sealed class ApexApiClient(HttpClient httpClient)
{
    private const string Shipments = "api/v2/shipments";
    private const string ScansSuffix = "/scans";
    private const string StatusSuffix = "/status";

    private static ApexJsonContext Json => ApexJson.Context;

    /// <summary>
    /// Name of the client registration (<c>AddHttpClient&lt;ApexApiClient&gt;(HttpClientName, ...)</c>), so a
    /// singleton such as the uploader can ask <see cref="IHttpClientFactory"/> for the same configured client.
    /// </summary>
    public const string HttpClientName = "ApexApi";

    private const string DevelopmentTokenPath = "dev/token";

    /// <summary>The API root this client calls (chosen per platform at start-up).</summary>
    public Uri? BaseAddress => httpClient.BaseAddress;

    /// <summary>
    /// Signs a fictional courier in through <c>POST /dev/token</c>, which exists only while the API runs in
    /// Development (404 elsewhere). The answer holds a one-hour Courier token.
    /// </summary>
    /// <param name="courierId">Courier id, <c>CR-</c> plus three digits (CR-101 or CR-102 in Development).</param>
    /// <param name="ct">Cancels the call.</param>
    public async Task<ApiResult<DevTokenResponse>> RequestDevelopmentTokenAsync(string courierId, CancellationToken ct)
    {
        if (!ScanRules.IsValidCourierId(courierId))
        {
            return Failed<DevTokenResponse>(ApiFailureKind.InvalidInput, FailureMessages.InvalidCourierId, null);
        }

        using var body = JsonContent.Create(new DevTokenRequest(courierId), Json.DevTokenRequest);
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(DevelopmentTokenPath, UriKind.Relative))
        {
            Content = body,
        };
        return await SendAsync(request, Json.DevTokenResponse, ct).ConfigureAwait(false);
    }

    /// <summary>Gets one shipment (<c>GET /api/v2/shipments/{trackingNumber}</c>).</summary>
    /// <param name="trackingNumber">Tracking number, <c>APX-</c> plus eight digits.</param>
    /// <param name="ct">Cancels the call.</param>
    public Task<ApiResult<ShipmentResponse>> GetShipmentAsync(string trackingNumber, CancellationToken ct) =>
        SendForShipmentAsync(trackingNumber, HttpMethod.Get, string.Empty, null, Json.ShipmentResponse, ct);

    /// <summary>Lists shipments (<c>GET /api/v2/shipments?page=&amp;pageSize=</c>).</summary>
    /// <param name="page">Page number, starting at 1.</param>
    /// <param name="pageSize">Page size; the API accepts 1–50.</param>
    /// <param name="ct">Cancels the call.</param>
    public async Task<ApiResult<ShipmentPage>> ListShipmentsAsync(int page, int pageSize, CancellationToken ct)
    {
        var path = string.Create(CultureInfo.InvariantCulture, $"{Shipments}?page={page}&pageSize={pageSize}");
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(path, UriKind.Relative));
        return await SendAsync(request, Json.ShipmentPage, ct).ConfigureAwait(false);
    }

    /// <summary>Gets the stored scans of a shipment, oldest first.</summary>
    /// <param name="trackingNumber">Tracking number, <c>APX-</c> plus eight digits.</param>
    /// <param name="ct">Cancels the call.</param>
    public Task<ApiResult<IReadOnlyList<ScanResponse>>> GetScansAsync(string trackingNumber, CancellationToken ct) =>
        SendForShipmentAsync(trackingNumber, HttpMethod.Get, ScansSuffix, null, Json.IReadOnlyListScanResponse, ct);

    /// <summary>
    /// Uploads scans of one shipment. The request is marked with <see cref="IdempotentRequest"/> so the retry
    /// strategy may repeat it: every scan carries its <c>scan::{Guid}</c> id and the server skips a repeated id.
    /// </summary>
    /// <param name="trackingNumber">Tracking number the scans belong to.</param>
    /// <param name="request">The batch.</param>
    /// <param name="ct">Cancels the call.</param>
    public Task<ApiResult<ScanUploadResult>> UploadScansAsync(
        string trackingNumber,
        ScanUploadRequest request,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var body = JsonContent.Create(request, Json.ScanUploadRequest);
        return SendForShipmentAsync(
            trackingNumber,
            HttpMethod.Post,
            ScansSuffix,
            body,
            Json.ScanUploadResult,
            ct,
            retrySafe: true);
    }

    /// <summary>Moves a shipment forward (<c>PATCH /api/v2/shipments/{trackingNumber}/status</c>).</summary>
    /// <param name="trackingNumber">Tracking number, <c>APX-</c> plus eight digits.</param>
    /// <param name="status">The new status.</param>
    /// <param name="ct">Cancels the call.</param>
    public Task<ApiResult<ShipmentResponse>> ChangeStatusAsync(
        string trackingNumber,
        ShipmentStatus status,
        CancellationToken ct)
    {
        var body = JsonContent.Create(new UpdateStatusRequest(status), Json.UpdateStatusRequest);
        return SendForShipmentAsync(trackingNumber, HttpMethod.Patch, StatusSuffix, body, Json.ShipmentResponse, ct);
    }

    private async Task<ApiResult<T>> SendForShipmentAsync<T>(
        string trackingNumber,
        HttpMethod method,
        string suffix,
        HttpContent? body,
        JsonTypeInfo<T> typeInfo,
        CancellationToken ct,
        bool retrySafe = false)
        where T : class
    {
        using (body)
        {
            // Checked here so a typo never leaves the device and never reaches a different route.
            if (!ScanRules.IsValidTrackingNumber(trackingNumber))
            {
                return Failed<T>(ApiFailureKind.InvalidInput, FailureMessages.InvalidTrackingNumber, null);
            }

            var path = new Uri($"{Shipments}/{trackingNumber}{suffix}", UriKind.Relative);
            using var request = new HttpRequestMessage(method, path) { Content = body };
            if (retrySafe)
            {
                IdempotentRequest.Mark(request);
            }

            return await SendAsync(request, typeInfo, ct).ConfigureAwait(false);
        }
    }

    private async Task<ApiResult<T>> SendAsync<T>(
        HttpRequestMessage request,
        JsonTypeInfo<T> typeInfo,
        CancellationToken ct)
        where T : class
    {
        try
        {
            using var response = await httpClient
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct)
                .ConfigureAwait(false);

            return response.IsSuccessStatusCode
                ? await ReadValueAsync(response, typeInfo, ct).ConfigureAwait(false)
                : ApiResult.Fail<T>(await ReadFailureAsync(response, ct).ConfigureAwait(false));
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // HttpClient.Timeout surfaces as a cancellation the caller did not ask for.
            return Failed<T>(ApiFailureKind.Timeout, FailureMessages.Timeout, null);
        }
        catch (HttpRequestException)
        {
            return Failed<T>(ApiFailureKind.Network, FailureMessages.Network, null);
        }
        catch (TimeoutRejectedException)
        {
            // The total timeout, or the attempt timeout of the last attempt, of the resilience pipeline ran out.
            return Failed<T>(ApiFailureKind.Timeout, FailureMessages.Timeout, null);
        }
        catch (BrokenCircuitException)
        {
            // The circuit is open: nothing was sent and the app keeps working offline.
            return Failed<T>(ApiFailureKind.ServerUnavailable, FailureMessages.ServerUnavailable, null);
        }
    }

    private static async Task<ApiResult<T>> ReadValueAsync<T>(
        HttpResponseMessage response,
        JsonTypeInfo<T> typeInfo,
        CancellationToken ct)
        where T : class
    {
        var status = (int)response.StatusCode;
        try
        {
            var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            await using (stream.ConfigureAwait(false))
            {
                var value = await JsonSerializer.DeserializeAsync(stream, typeInfo, ct).ConfigureAwait(false);
                return value is null
                    ? Failed<T>(ApiFailureKind.InvalidResponse, FailureMessages.InvalidResponse, status)
                    : ApiResult.Success(value);
            }
        }
        catch (JsonException)
        {
            // Empty or malformed JSON, a null or missing required value, or an unknown enum name.
            return Failed<T>(ApiFailureKind.InvalidResponse, FailureMessages.InvalidResponse, status);
        }
    }

    private static async Task<ApiFailure> ReadFailureAsync(HttpResponseMessage response, CancellationToken ct)
    {
        ApiProblem? problem = null;
        var mediaType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
        if (mediaType.Contains("json", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                problem = await response.Content.ReadFromJsonAsync(Json.ApiProblem, ct).ConfigureAwait(false);
            }
            catch (JsonException)
            {
                // An unreadable error body still yields the status-based message.
                problem = null;
            }
        }

        return FailureMessages.FromProblem(response.StatusCode, problem);
    }

    private static ApiResult<T> Failed<T>(ApiFailureKind kind, string message, int? status)
        where T : class =>
        ApiResult.Fail<T>(new ApiFailure(kind, message, status));
}
