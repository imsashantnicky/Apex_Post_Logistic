namespace ApexPost.Courier;

/// <summary>The courier application; it shows <see cref="MainPage"/> in a single window.</summary>
public partial class App : Application
{
    private readonly MainPage _mainPage;

    /// <summary>Creates the app with its start page from dependency injection.</summary>
    /// <param name="mainPage">The start page.</param>
    public App(MainPage mainPage)
    {
        InitializeComponent();
        _mainPage = mainPage;
    }

    /// <inheritdoc />
    protected override Window CreateWindow(IActivationState? activationState) =>
        new(new NavigationPage(_mainPage)) { Title = "ApexPost Courier" };
}
