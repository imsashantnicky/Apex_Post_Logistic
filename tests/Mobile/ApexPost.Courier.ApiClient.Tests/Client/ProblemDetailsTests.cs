using System.Net;
using ApexPost.Courier.ApiClient.Results;
using ApexPost.Courier.ApiClient.Tests.Support;

namespace ApexPost.Courier.ApiClient.Tests.Client;

public sealed class ProblemDetailsTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_404_problem_shows_the_server_detail_and_keeps_the_trace_id()
    {
        var handler = FakeHttpMessageHandler.Returning(HttpStatusCode.NotFound, SampleJson.NotFoundProblem, SampleJson.ProblemJson);

        var result = await ClientFactory.Create(handler).GetShipmentAsync("APX-00000000", Ct);

        Assert.Equal(ApiFailureKind.NotFound, result.Failure?.Kind);
        Assert.Equal("Shipment APX-00000000 was not found.", result.Failure?.Message);
        Assert.Equal(404, result.Failure?.StatusCode);
        Assert.Equal("00-abc-def-01", result.Failure?.TraceId);
    }

    [Fact]
    public async Task A_validation_problem_lists_the_field_errors_in_a_friendly_message()
    {
        var handler = FakeHttpMessageHandler.Returning(HttpStatusCode.BadRequest, SampleJson.ValidationProblem, SampleJson.ProblemJson);

        var result = await ClientFactory.Create(handler)
            .UploadScansAsync("APX-20000005", new Contracts.ScanUploadRequest([]), Ct);

        var failure = Assert.IsType<ApiFailure>(result.Failure);
        Assert.Equal(ApiFailureKind.Validation, failure.Kind);
        Assert.Equal(
            "Please check: The Location field is required. A Delivered scan needs the recipient's signature name.",
            failure.Message);
        Assert.Equal(2, failure.FieldErrors.Count);
        Assert.Equal(["The Location field is required."], failure.FieldErrors["Scans[0].Location"]);
        Assert.Equal("00-abc-def-02", failure.TraceId);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, ApiFailureKind.Unauthorized, "Your sign-in has expired or is missing. Sign in again.")]
    [InlineData(HttpStatusCode.Forbidden, ApiFailureKind.Forbidden, "Your account is not allowed to do this.")]
    [InlineData(HttpStatusCode.InternalServerError, ApiFailureKind.Server, "The ApexPost server had a problem. Try again later.")]
    [InlineData(HttpStatusCode.BadGateway, ApiFailureKind.Server, "The ApexPost server had a problem. Try again later.")]
    public async Task Auth_and_server_errors_use_fixed_texts_and_never_show_the_server_detail(
        HttpStatusCode status,
        ApiFailureKind kind,
        string message)
    {
        const string body = """{ "title": "Error", "detail": "System.InvalidOperationException at Line 42", "traceId": "t-1" }""";
        var handler = FakeHttpMessageHandler.Returning(status, body, SampleJson.ProblemJson);

        var result = await ClientFactory.Create(handler).GetShipmentAsync("APX-20000005", Ct);

        Assert.Equal(kind, result.Failure?.Kind);
        Assert.Equal(message, result.Failure?.Message);
        Assert.Equal("t-1", result.Failure?.TraceId);
    }

    [Fact]
    public async Task A_409_problem_shows_the_server_detail()
    {
        const string body = """{ "title": "Conflict", "status": 409, "detail": "Cannot move from Delivered to InTransit." }""";
        var handler = FakeHttpMessageHandler.Returning(HttpStatusCode.Conflict, body, SampleJson.ProblemJson);

        var result = await ClientFactory.Create(handler)
            .ChangeStatusAsync("APX-20000005", Contracts.ShipmentStatus.InTransit, Ct);

        Assert.Equal(ApiFailureKind.Conflict, result.Failure?.Kind);
        Assert.Equal("Cannot move from Delivered to InTransit.", result.Failure?.Message);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, "{ broken", SampleJson.ProblemJson, ApiFailureKind.NotFound)]
    [InlineData(HttpStatusCode.NotFound, "<html>404</html>", "text/html", ApiFailureKind.NotFound)]
    [InlineData(HttpStatusCode.Conflict, null, SampleJson.ProblemJson, ApiFailureKind.Conflict)]
    [InlineData(HttpStatusCode.BadRequest, """{ "errors": null, "detail": null }""", SampleJson.ProblemJson, ApiFailureKind.Validation)]
    [InlineData(HttpStatusCode.UnsupportedMediaType, "[]", SampleJson.ProblemJson, ApiFailureKind.Rejected)]
    public async Task A_missing_or_unreadable_problem_body_still_gives_a_status_based_message(
        HttpStatusCode status,
        string? body,
        string mediaType,
        ApiFailureKind kind)
    {
        var handler = FakeHttpMessageHandler.Returning(status, body, mediaType);

        var result = await ClientFactory.Create(handler).GetShipmentAsync("APX-20000005", Ct);

        Assert.Equal(kind, result.Failure?.Kind);
        Assert.False(string.IsNullOrWhiteSpace(result.Failure?.Message));
        Assert.Equal((int)status, result.Failure?.StatusCode);
        Assert.Null(result.Failure?.TraceId);
    }

    [Fact]
    public void Null_field_messages_are_dropped_and_the_field_name_is_shown_instead()
    {
        var problem = new Contracts.ApiProblem
        {
            Errors = new Dictionary<string, string[]> { ["$.scans[0].scanType"] = [null!, " "] },
        };

        var failure = FailureMessages.FromProblem(HttpStatusCode.BadRequest, problem);

        Assert.Equal("Please check: $.scans[0].scanType", failure.Message);
        Assert.Empty(failure.FieldErrors["$.scans[0].scanType"]);
    }
}
