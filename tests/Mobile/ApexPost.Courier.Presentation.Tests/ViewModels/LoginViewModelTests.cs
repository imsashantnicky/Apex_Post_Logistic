using System.Net;
using ApexPost.Courier.ApiClient.Results;
using ApexPost.Courier.Presentation.Tests.Support;
using ApexPost.Courier.Presentation.ViewModels;

namespace ApexPost.Courier.Presentation.Tests.ViewModels;

public sealed class LoginViewModelTests
{
    private const string NotDevelopmentProblem = """
        { "title": "Not Found", "status": 404, "detail": "No such endpoint.", "traceId": "00-dev-404-01" }
        """;

    private readonly InMemoryTokenStore _tokens = new();
    private readonly FakeNavigator _navigator = new();
    private readonly FakeSyncRunner _sync = new();

    [Fact]
    public async Task Dev_sign_in_stores_the_token_opens_the_tabs_and_starts_a_sync()
    {
        var token = TestApi.CourierToken("CR-102");
        var handler = new CannedHandler(
            HttpStatusCode.OK,
            $$"""{"accessToken":"{{token}}","tokenType":"Bearer","expiresAtUtc":"2026-11-07T12:00:00Z","courierId":"CR-102"}""",
            TestApi.Json);
        var viewModel = Create(handler);
        viewModel.CourierId = " cr-102 ";

        await viewModel.SignInCommand.ExecuteAsync();

        Assert.Equal(token, _tokens.Token);
        Assert.Equal(["main"], _navigator.Visits);
        Assert.Equal(["sign-in"], _sync.Triggers);
        Assert.Equal("CR-102", viewModel.CourierId);
        Assert.False(viewModel.HasMessage);
    }

    [Theory]
    [InlineData("")]
    [InlineData("CR-1")]
    [InlineData("XX-101")]
    public async Task A_malformed_courier_id_is_refused_before_any_request(string courierId)
    {
        var handler = new CannedHandler(HttpStatusCode.OK, "{}", TestApi.Json);
        var viewModel = Create(handler);
        viewModel.CourierId = courierId;

        await viewModel.SignInCommand.ExecuteAsync();

        Assert.Empty(handler.Requests);
        Assert.Equal(FailureMessages.InvalidCourierId, viewModel.Message);
        Assert.True(viewModel.MessageIsError);
        Assert.Null(_tokens.Token);
        Assert.Empty(_navigator.Visits);
    }

    [Fact]
    public async Task A_problem_details_answer_is_shown_as_friendly_text_with_the_trace_id()
    {
        var viewModel = Create(new CannedHandler(HttpStatusCode.NotFound, NotDevelopmentProblem, TestApi.Problem));

        await viewModel.SignInCommand.ExecuteAsync();

        Assert.True(viewModel.MessageIsError);
        Assert.StartsWith("No such endpoint.", viewModel.Message, StringComparison.Ordinal);
        Assert.Contains("Reference: 00-dev-404-01", viewModel.Message, StringComparison.Ordinal);
        Assert.Null(_tokens.Token);
    }

    [Fact]
    public async Task A_pasted_courier_token_is_stored_and_cleared_from_the_field()
    {
        var token = TestApi.CourierToken("CR-101");
        var viewModel = Create(new CannedHandler(HttpStatusCode.OK, "{}", TestApi.Json));
        viewModel.Token = $"  {token}  ";

        await viewModel.UseTokenCommand.ExecuteAsync();

        Assert.Equal(token, _tokens.Token);
        Assert.Equal(string.Empty, viewModel.Token);
        Assert.Equal(["main"], _navigator.Visits);
    }

    [Theory]
    [InlineData("not-a-jwt")]
    [InlineData("eyJhbGciOiJIUzI1NiJ9.eyJyb2xlIjoiQWRtaW4ifQ.sig")]
    public async Task A_pasted_token_without_a_courier_id_is_refused(string token)
    {
        var viewModel = Create(new CannedHandler(HttpStatusCode.OK, "{}", TestApi.Json));
        viewModel.Token = token;

        await viewModel.UseTokenCommand.ExecuteAsync();

        Assert.Equal(LoginViewModel.NotACourierToken, viewModel.Message);
        Assert.Null(_tokens.Token);
        Assert.Equal(string.Empty, viewModel.Token);
    }

    [Fact]
    public void ShowError_raises_property_changes_for_the_page()
    {
        var viewModel = Create(new CannedHandler(HttpStatusCode.OK, "{}", TestApi.Json));
        var changed = new List<string?>();
        viewModel.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        viewModel.ShowError("Your session has expired.");

        Assert.Contains(nameof(LoginViewModel.Message), changed);
        Assert.Contains(nameof(LoginViewModel.HasMessage), changed);
        Assert.Contains(nameof(LoginViewModel.MessageIsError), changed);
    }

    private LoginViewModel Create(CannedHandler handler) =>
        new(TestApi.Client(handler), _tokens, _navigator, _sync);
}
