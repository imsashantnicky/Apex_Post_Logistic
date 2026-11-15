using System.Net;
using ApexPost.Api.Errors;
using ApexPost.Api.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace ApexPost.Api.Tests.Errors;

/// <summary>Outside Development the API shows no stack trace and never prepares the database by itself.</summary>
public sealed class ProductionErrorTests(ProductionApiFactory factory) : IClassFixture<ProductionApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Unexpected_error_in_Production_hides_the_stack_trace_and_the_message()
    {
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/test/throw/unexpected", Ct);
        var body = await response.Content.ReadAsStringAsync(Ct);

        var problem = await Problem.ReadAsync(response, HttpStatusCode.InternalServerError);

        Assert.False(problem.Has(ApiExceptionHandler.StackTraceKey));
        Assert.Equal(ApiExceptionHandler.UnexpectedDetail, problem.Detail);
        Assert.DoesNotContain(ThrowingController.SecretMessage, body, StringComparison.Ordinal);
        Assert.False(string.IsNullOrWhiteSpace(problem.TraceId));
    }

    [Fact]
    public async Task Expected_error_in_Production_has_no_stack_trace()
    {
        using var client = factory.CreateClient();

        var problem = await Problem.ReadAsync(
            await client.GetAsync("/test/throw/not-found", Ct),
            HttpStatusCode.NotFound);

        Assert.False(problem.Has(ApiExceptionHandler.StackTraceKey));
    }

    [Fact]
    public async Task Production_start_neither_migrates_nor_seeds_the_database()
    {
        await using var database = factory.OpenTrackingDatabase();

        Assert.False(await database.Database.CanConnectAsync(Ct));
    }
}
