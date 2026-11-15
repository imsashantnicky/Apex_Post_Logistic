using ApexPost.Courier.ApiClient.Results;
using ApexPost.Courier.Presentation.Errors;

namespace ApexPost.Courier.Presentation.Tests.ViewModels;

public sealed class FriendlyErrorTests
{
    [Fact]
    public void A_failure_with_a_trace_id_gets_a_reference_line()
    {
        var failure = new ApiFailure(ApiFailureKind.NotFound, "No shipment was found.", 404, "00-abc-01");

        Assert.Equal($"No shipment was found.{Environment.NewLine}Reference: 00-abc-01", FriendlyError.Describe(failure));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void A_failure_without_a_trace_id_is_shown_as_it_is(string? traceId)
    {
        var failure = new ApiFailure(ApiFailureKind.Network, FailureMessages.Network, null, traceId);

        Assert.Equal(FailureMessages.Network, FriendlyError.Describe(failure));
    }
}
