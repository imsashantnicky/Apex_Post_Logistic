namespace ApexPost.Courier;

/// <summary>Switches the window between the sign-in page and the main page, always on the UI thread.</summary>
/// <param name="services">Creates the pages (both are transient).</param>
public sealed class AppNavigator(IServiceProvider services)
{
    /// <summary>Shows the sign-in page, optionally with a message such as "session expired".</summary>
    /// <param name="message">Text shown above the sign-in cards, or <see langword="null"/>.</param>
    public Task ShowLoginAsync(string? message = null) =>
        ShowAsync<LoginPage>(page =>
        {
            if (message is not null)
            {
                page.ShowMessage(message);
            }
        });

    /// <summary>Shows the main page.</summary>
    public Task ShowMainAsync() => ShowAsync<MainPage>(_ => { });

    private Task ShowAsync<TPage>(Action<TPage> prepare)
        where TPage : Page =>
        MainThread.InvokeOnMainThreadAsync(() =>
        {
            if (Application.Current?.Windows is [var window, ..])
            {
                var page = services.GetRequiredService<TPage>();
                prepare(page);
                window.Page = new NavigationPage(page);
            }
        });
}
