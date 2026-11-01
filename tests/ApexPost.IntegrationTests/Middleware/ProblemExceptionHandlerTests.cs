using System.Text;
using ApexPost.Application.Exceptions;
using ApexPost.Domain.Common;
using ApexPost.Web.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Logging.Abstractions;

namespace ApexPost.IntegrationTests.Middleware;

public sealed class ProblemExceptionHandlerTests
{
    private const string Internal = "internal detail";

    public static TheoryData<Exception, int> Mapped => new()
    {
        { new NotFoundException(Internal), StatusCodes.Status404NotFound },
        { new ForbiddenException(Internal), StatusCodes.Status403Forbidden },
        { new ConcurrencyException(Internal), StatusCodes.Status409Conflict },
        { new ExternalServiceTimeoutException(Internal), StatusCodes.Status504GatewayTimeout },
        { new ExternalServiceException(Internal), StatusCodes.Status502BadGateway },
        { new InvalidOperationException(Internal), StatusCodes.Status500InternalServerError },
    };

    [Theory]
    [MemberData(nameof(Mapped))]
    public async Task Exception_maps_to_its_status_without_showing_its_message(Exception exception, int status)
    {
        var (context, body) = await HandleAsync(exception, Environments.Production);

        Assert.Equal(status, context.Response.StatusCode);
        Assert.DoesNotContain(Internal, body, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(typeof(ValidationFailedException))]
    [InlineData(typeof(DomainException))]
    public async Task Rule_messages_are_shown_with_400(Type type)
    {
        var exception = (Exception)Activator.CreateInstance(type, "The amount is not valid.")!;

        var (context, body) = await HandleAsync(exception, Environments.Production);

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        Assert.Contains("The amount is not valid.", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Stack_trace_is_added_only_in_development()
    {
        var failure = new InvalidOperationException(Internal);

        var (_, development) = await HandleAsync(failure, Environments.Development);
        var (_, production) = await HandleAsync(failure, Environments.Production);

        Assert.Contains(ProblemResponses.StackTraceKey, development, StringComparison.Ordinal);
        Assert.DoesNotContain(ProblemResponses.StackTraceKey, production, StringComparison.Ordinal);
    }

    private static async Task<(HttpContext Context, string Body)> HandleAsync(Exception exception, string environment)
    {
        using var body = new MemoryStream();
        using var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = services };
        context.Response.Body = body;
        var handler = new ProblemExceptionHandler(
            NullLogger<ProblemExceptionHandler>.Instance,
            new HostingEnvironment { EnvironmentName = environment });

        var handled = await handler.TryHandleAsync(context, exception, TestContext.Current.CancellationToken);

        Assert.True(handled);
        return (context, Encoding.UTF8.GetString(body.ToArray()));
    }
}
