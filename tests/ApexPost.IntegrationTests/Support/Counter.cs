using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;

namespace ApexPost.IntegrationTests.Support;

/// <summary>The steps of the counter workflow, each posted like the browser does.</summary>
internal sealed partial class Counter(CounterBrowser browser, string path)
{
    public const string TransactionsPath = "/Transactions";
    private const string NewPath = "/Transactions/New";

    public string Path => path;

    public string Id => path[(path.LastIndexOf('/') + 1)..];

    public static async Task<Counter> OpenAsync(CounterBrowser browser)
    {
        using var response = await PostOpenAsync(browser);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return new Counter(browser, response.Headers.Location!.OriginalString);
    }

    /// <summary>Posts the new-transaction form with the first counter of the branch.</summary>
    public static async Task<HttpResponseMessage> PostOpenAsync(CounterBrowser browser)
    {
        var page = await browser.ReadPageAsync(NewPath);
        var counterId = CounterOption().Match(page).Groups[1].Value;
        return await browser.PostAsync(
            NewPath,
            $"{TransactionsPath}/Create",
            new Dictionary<string, string> { ["counterId"] = counterId });
    }

    public Counter For(CounterBrowser other) => new(other, path);

    public Task<string> PageAsync() => browser.ReadPageAsync(path);

    public Task<HttpResponseMessage> AddRetailAsync(string sku, int quantity = 1) =>
        PostAsync("AddRetail", new() { ["Sku"] = sku, ["Quantity"] = Text(quantity) });

    public Task<HttpResponseMessage> AddParcelAsync(
        decimal weightKg = 2m,
        string level = "Standard",
        string type = "Parcel") =>
        PostAsync("AddShipping", new()
        {
            ["Type"] = type,
            ["Level"] = level,
            ["WeightKg"] = Text(weightKg),
            ["LengthCm"] = "30",
            ["WidthCm"] = "20",
            ["HeightCm"] = "10",
            ["ZoneCode"] = "Z01",
            ["RecipientName"] = "Hannah Reid",
            ["Street"] = "12 Mill Lane",
            ["City"] = "Bristol",
            ["PostalCode"] = "BS1 4DJ",
            ["Country"] = "United Kingdom",
        });

    public Task<HttpResponseMessage> AddMoneyOrderAsync(decimal amount) =>
        PostAsync("AddMoneyOrder", new()
        {
            ["Amount"] = Text(amount),
            ["Sender"] = "Sam Taylor",
            ["Recipient"] = "Priya Nair",
        });

    public Task<HttpResponseMessage> VerifyAsync(string documentNumber) =>
        PostAsync("VerifyIdentity", new() { ["DocumentType"] = "Passport", ["DocumentNumber"] = documentNumber });

    public Task<HttpResponseMessage> PayAsync(string method = "Card") =>
        PostAsync("Pay", new() { ["method"] = method });

    public Task<HttpResponseMessage> VoidAsync(string reason) => PostAsync("Void", new() { ["Reason"] = reason });

    public Task<HttpResponseMessage> RefundAsync(string reason) => PostAsync("Refund", new() { ["Reason"] = reason });

    /// <summary>Pays another transaction with the token of this counter's page, as a stale tab would.</summary>
    public Task<HttpResponseMessage> PayOtherAsync(Guid transactionId) =>
        browser.PostAsync(path, $"{TransactionsPath}/Pay/{transactionId}", new Dictionary<string, string>
        {
            ["method"] = "Card",
        });

    /// <summary>Voids a transaction whose page this browser may not open, with the token of its own page.</summary>
    public Task<HttpResponseMessage> VoidOtherAsync(string transactionId, string reason) =>
        browser.PostAsync(path, $"{TransactionsPath}/Void/{transactionId}", new Dictionary<string, string>
        {
            ["Reason"] = reason,
        });

    /// <summary>The link to the first label preview on the counter screen.</summary>
    public async Task<string> LabelLinkAsync()
    {
        var link = LabelLink().Match(await PageAsync());
        Assert.True(link.Success, "The counter screen has no label link.");
        return WebUtility.HtmlDecode(link.Groups[1].Value);
    }

    private static string Text<T>(T value)
        where T : IFormattable =>
        value.ToString(null, CultureInfo.InvariantCulture);

    private async Task<HttpResponseMessage> PostAsync(string action, Dictionary<string, string> fields)
    {
        return await browser.PostAsync(path, $"{TransactionsPath}/{action}/{Id}", fields);
    }

    [GeneratedRegex("<option value=\"([0-9a-f-]{36})\"")]
    private static partial Regex CounterOption();

    [GeneratedRegex("href=\"(/Transactions/Label/[^\"]+)\"")]
    private static partial Regex LabelLink();
}
