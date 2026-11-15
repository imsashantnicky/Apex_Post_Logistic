using ApexPost.Courier.Presentation.Ports;

namespace ApexPost.Courier.Services;

/// <summary>Moves between the sign-in route and the main tabs with Shell navigation, always on the UI thread.</summary>
public sealed class ShellNavigator : IAppNavigator
{
    /// <inheritdoc />
    public Task ShowLoginAsync() => GoToAsync(AppShell.LoginRoute);

    /// <inheritdoc />
    public Task ShowMainAsync() => GoToAsync(AppShell.MainRoute);

    private static Task GoToAsync(string route) =>
        MainThread.InvokeOnMainThreadAsync(() =>
            Shell.Current is { } shell ? shell.GoToAsync(route) : Task.CompletedTask);
}
