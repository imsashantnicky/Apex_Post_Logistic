namespace ApexPost.Courier;

/// <summary>Shell of the app: the sign-in page and the three main tabs (New Scan, Pending, Sync Status).</summary>
public partial class AppShell : Shell
{
    /// <summary>Route of the sign-in page.</summary>
    public const string LoginRoute = "//login";

    /// <summary>Route of the main tabs; opens the first tab (New Scan).</summary>
    public const string MainRoute = "//main";

    /// <summary>Creates the shell.</summary>
    public AppShell() => InitializeComponent();
}
