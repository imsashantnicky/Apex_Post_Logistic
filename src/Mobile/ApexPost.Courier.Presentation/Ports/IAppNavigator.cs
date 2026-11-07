namespace ApexPost.Courier.Presentation.Ports;

/// <summary>Moves between the sign-in page and the main tabs (Shell routes in the app, a fake in tests).</summary>
public interface IAppNavigator
{
    /// <summary>Shows the sign-in page.</summary>
    Task ShowLoginAsync();

    /// <summary>Shows the main tabs, starting with New Scan.</summary>
    Task ShowMainAsync();
}
