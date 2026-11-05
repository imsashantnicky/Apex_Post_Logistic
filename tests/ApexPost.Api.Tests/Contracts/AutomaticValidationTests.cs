using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ApexPost.Api.Contracts;
using ApexPost.Api.Tests.Support;

namespace ApexPost.Api.Tests.Contracts;

/// <summary>
/// Through the real host, [ApiController] answers an invalid body with 400 ValidationProblemDetails by itself,
/// including the custom rules on nested scans; a valid body reaches the action.
/// </summary>
public sealed class AutomaticValidationTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_valid_create_body_reaches_the_action()
    {
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/test/contracts/shipments", Requests.Create(), Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task An_invalid_create_body_gets_400_with_an_error_per_field()
    {
        using var client = factory.CreateClient();
        var body = new
        {
            trackingNumber = "APX-1",
            recipientName = "",
            recipientPhone = "phone",
            recipientAddress = "1 Road",
            recipientCity = "Northgate",
            weightKg = 31,
        };

        var problem = await Problem.ReadAsync(
            await client.PostAsJsonAsync("/test/contracts/shipments", body, Ct),
            HttpStatusCode.BadRequest);

        var errors = Errors(problem);
        Assert.Contains(ContractRules.TrackingNumberMessage, errors["TrackingNumber"]);
        Assert.Contains(ContractRules.PhoneMessage, errors["RecipientPhone"]);
        Assert.Contains(ContractRules.WeightMessage, errors["WeightKg"]);
        Assert.True(errors.ContainsKey("RecipientName"));
        Assert.False(string.IsNullOrWhiteSpace(problem.TraceId));
    }

    [Fact]
    public async Task A_Delivered_scan_without_a_signature_gets_400_on_that_scan()
    {
        using var client = factory.CreateClient();
        var body = new { scans = new[] { Scan(scanType: 3, scannedAtUtc: DateTime.UtcNow.AddMinutes(-5)) } };

        var problem = await Problem.ReadAsync(
            await client.PostAsJsonAsync("/test/contracts/scans", body, Ct),
            HttpStatusCode.BadRequest);

        Assert.Contains(ContractRules.SignatureMessage, Errors(problem)["Scans[0].RecipientSignatureName"]);
    }

    [Fact]
    public async Task A_scan_from_the_future_gets_400_on_scannedAtUtc()
    {
        using var client = factory.CreateClient();
        var body = new { scans = new[] { Scan(scanType: 1, scannedAtUtc: DateTime.UtcNow.AddMinutes(10)) } };

        var problem = await Problem.ReadAsync(
            await client.PostAsJsonAsync("/test/contracts/scans", body, Ct),
            HttpStatusCode.BadRequest);

        var message = Errors(problem)["Scans[0].ScannedAtUtc"][0];
        Assert.Contains("must not be in the future", message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_valid_scan_upload_reaches_the_action()
    {
        using var client = factory.CreateClient();
        var body = new { scans = new[] { Scan(scanType: 1, scannedAtUtc: DateTime.UtcNow.AddMinutes(1)) } };

        var response = await client.PostAsJsonAsync("/test/contracts/scans", body, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task An_empty_upload_gets_400()
    {
        using var client = factory.CreateClient();

        var problem = await Problem.ReadAsync(
            await client.PostAsJsonAsync("/test/contracts/scans", new { scans = Array.Empty<object>() }, Ct),
            HttpStatusCode.BadRequest);

        Assert.True(Errors(problem).ContainsKey("Scans"));
    }

    [Fact]
    public async Task A_status_change_without_a_status_gets_400()
    {
        using var client = factory.CreateClient();

        var problem = await Problem.ReadAsync(
            await client.PatchAsJsonAsync("/test/contracts/status", new { }, Ct),
            HttpStatusCode.BadRequest);

        Assert.True(Errors(problem).ContainsKey("Status"));
    }

    private static object Scan(int scanType, DateTime scannedAtUtc) => new
    {
        scanId = Requests.ScanId,
        trackingNumber = "APX-20000001",
        scanType,
        courierId = "CR-101",
        location = "Central Hub",
        scannedAtUtc = scannedAtUtc.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
    };

    private static Dictionary<string, string[]> Errors(Problem problem) =>
        problem.Root.GetProperty("errors").Deserialize<Dictionary<string, string[]>>()
        ?? throw new InvalidOperationException("No errors in the problem.");
}
