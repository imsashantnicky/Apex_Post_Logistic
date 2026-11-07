using ApexPost.Courier.ApiClient.Resilience;
using ApexPost.Courier.ApiClient.Results;
using ApexPost.Courier.Presentation.Tests.Support;
using ApexPost.Courier.Presentation.ViewModels;
using Polly.CircuitBreaker;

namespace ApexPost.Courier.Presentation.Tests.ViewModels;

public sealed class ConnectionBannerViewModelTests
{
    private readonly CircuitStateTracker _circuit = new();
    private readonly InlineDispatcher _dispatcher = new();

    [Fact]
    public void The_banner_is_hidden_while_the_circuit_is_closed()
    {
        using var banner = new ConnectionBannerViewModel(_circuit, _dispatcher);

        Assert.False(banner.IsOffline);
        Assert.Equal(CircuitState.Closed, banner.CircuitState);
        Assert.StartsWith("Closed", banner.CircuitStateText, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(CircuitState.Open, true)]
    [InlineData(CircuitState.Isolated, true)]
    [InlineData(CircuitState.HalfOpen, false)]
    public void The_banner_follows_the_circuit_through_the_dispatcher(CircuitState state, bool offline)
    {
        using var banner = new ConnectionBannerViewModel(_circuit, _dispatcher);

        _circuit.Update(state);

        Assert.Equal(1, _dispatcher.Posted);
        Assert.Equal(offline, banner.IsOffline);
        Assert.Equal(state, banner.CircuitState);
    }

    [Fact]
    public void The_banner_hides_again_when_the_circuit_closes()
    {
        using var banner = new ConnectionBannerViewModel(_circuit, _dispatcher);
        var changed = new List<string?>();
        banner.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        _circuit.Update(CircuitState.Open);
        _circuit.Update(CircuitState.HalfOpen);
        _circuit.Update(CircuitState.Closed);

        Assert.False(banner.IsOffline);
        Assert.Contains(nameof(ConnectionBannerViewModel.CircuitStateText), changed);
        Assert.Equal(2, changed.Count(name => name == nameof(ConnectionBannerViewModel.IsOffline)));
    }

    [Fact]
    public void A_banner_created_while_the_circuit_is_open_starts_visible()
    {
        _circuit.Update(CircuitState.Open);

        using var banner = new ConnectionBannerViewModel(_circuit, _dispatcher);

        Assert.True(banner.IsOffline);
    }

    [Fact]
    public void The_banner_text_is_the_offline_message()
    {
        Assert.Equal(FailureMessages.ServerUnavailable, ConnectionBannerViewModel.Message);
        Assert.StartsWith("Server unavailable – working offline", ConnectionBannerViewModel.Message, StringComparison.Ordinal);
    }
}
