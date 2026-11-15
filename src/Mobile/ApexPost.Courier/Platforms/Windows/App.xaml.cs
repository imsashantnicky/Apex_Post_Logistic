namespace ApexPost.Courier.WinUI;

/// <summary>Windows (WinUI) application object; it builds the MAUI app.</summary>
public partial class App : MauiWinUIApplication
{
    /// <summary>Initialises the WinUI application.</summary>
    public App()
    {
        InitializeComponent();
    }

    /// <inheritdoc />
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
