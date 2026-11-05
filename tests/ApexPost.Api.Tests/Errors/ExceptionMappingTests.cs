using System.Net;
using System.Net.Http.Json;
using ApexPost.Api.Errors;
using ApexPost.Api.Tests.Support;

namespace ApexPost.Api.Tests.Errors;

/// <summary>Every exception becomes ProblemDetails with the right status, a trace id and, in Development, a stack trace.</summary>
public sealed class ExceptionMappingTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("not-found", HttpStatusCode.NotFound, "Not found")]
    [InlineData("duplicate", HttpStatusCode.Conflict, "Duplicate")]
    [InlineData("invalid-transition", HttpStatusCode.Conflict, "Invalid status transition")]
    [InlineData("locked", HttpStatusCode.Conflict, "Shipment cannot change")]
    [InlineData("concurrency", HttpStatusCode.Conflict, "Changed by someone else")]
    [InlineData("validation", HttpStatusCode.BadRequest, "Validation failed")]
    [InlineData("domain", HttpStatusCode.BadRequest, "Business rule violated")]
    [InlineData("forbidden", HttpStatusCode.Forbidden, "Forbidden")]
    [InlineData("unexpected", HttpStatusCode.InternalServerError, "Server error")]
    public async Task Each_exception_maps_to_its_status_as_ProblemDetails(
        string kind,
        HttpStatusCode expected,
        string title)
    {
        using var client = factory.CreateClient();

        var problem = await Problem.ReadAsync(await client.GetAsync($"/test/throw/{kind}", Ct), expected);

        Assert.Equal(title, problem.Title);
        Assert.False(string.IsNullOrWhiteSpace(problem.TraceId));
        Assert.Equal($"/test/throw/{kind}", problem.Text("instance"));
    }

    [Fact]
    public async Task Expected_errors_show_the_message_written_by_the_service()
    {
        using var client = factory.CreateClient();

        var problem = await Problem.ReadAsync(
            await client.GetAsync("/test/throw/invalid-transition", Ct),
            HttpStatusCode.Conflict);

        Assert.Contains("cannot move from Delivered to InTransit", problem.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Validation_failure_lists_the_errors_per_field()
    {
        using var client = factory.CreateClient();

        var problem = await Problem.ReadAsync(
            await client.GetAsync("/test/throw/validation", Ct),
            HttpStatusCode.BadRequest);

        var errors = problem.Root.GetProperty("errors").GetProperty("pageSize");
        Assert.Equal("Page size must be between 1 and 50.", errors[0].GetString());
    }

    [Fact]
    public async Task Unexpected_error_in_Development_has_a_stack_trace_but_a_generic_detail()
    {
        using var client = factory.CreateClient();

        var problem = await Problem.ReadAsync(
            await client.GetAsync("/test/throw/unexpected", Ct),
            HttpStatusCode.InternalServerError);

        Assert.Equal(ApiExceptionHandler.UnexpectedDetail, problem.Detail);
        Assert.Contains(nameof(InvalidOperationException), problem.Text(ApiExceptionHandler.StackTraceKey));
    }

    [Fact]
    public async Task Invalid_model_returns_ValidationProblemDetails_with_a_trace_id()
    {
        using var client = factory.CreateClient();

        var problem = await Problem.ReadAsync(
            await client.PostAsJsonAsync("/test/echo", new { name = (string?)null }, Ct),
            HttpStatusCode.BadRequest);

        Assert.True(problem.Root.GetProperty("errors").TryGetProperty("Name", out _));
        Assert.False(string.IsNullOrWhiteSpace(problem.TraceId));
    }

    [Fact]
    public async Task Unknown_route_returns_404_ProblemDetails()
    {
        using var client = factory.CreateClient();

        var problem = await Problem.ReadAsync(await client.GetAsync("/no/such/route", Ct), HttpStatusCode.NotFound);

        Assert.False(string.IsNullOrWhiteSpace(problem.TraceId));
    }
}
