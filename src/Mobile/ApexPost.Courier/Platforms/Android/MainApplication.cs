using Android.App;
using Android.Runtime;

namespace ApexPost.Courier;

/// <summary>Android application object; it builds the MAUI app.</summary>
/// <param name="handle">Native handle.</param>
/// <param name="ownership">Handle ownership.</param>
[Application]
public class MainApplication(IntPtr handle, JniHandleOwnership ownership) : MauiApplication(handle, ownership)
{
    /// <inheritdoc />
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
