namespace ApexPost.Courier.Views;

/// <summary>"Server unavailable – working offline", visible while the API circuit breaker is open.</summary>
public partial class OfflineBanner : ContentView
{
    /// <summary>Creates the banner; its binding context is the shared banner view model.</summary>
    public OfflineBanner() => InitializeComponent();
}
