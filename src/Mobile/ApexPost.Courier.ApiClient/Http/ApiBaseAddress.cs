namespace ApexPost.Courier.ApiClient.Http;

/// <summary>Chooses the API base address for the platform from the embedded <c>appsettings.json</c>.</summary>
public static class ApiBaseAddress
{
    /// <summary>Configuration key of the Android address (the emulator reaches the host PC on 10.0.2.2).</summary>
    public const string AndroidKey = "Api:BaseUrl:Android";

    /// <summary>Configuration key of the Windows address.</summary>
    public const string WindowsKey = "Api:BaseUrl:Windows";

    /// <summary>Returns the configured address for the platform, as an absolute HTTPS URI ending in "/".</summary>
    /// <param name="androidValue">Value of <see cref="AndroidKey"/>.</param>
    /// <param name="windowsValue">Value of <see cref="WindowsKey"/>.</param>
    /// <param name="isAndroid">Whether the app runs on Android.</param>
    /// <exception cref="InvalidOperationException">The value is missing, relative or not HTTPS.</exception>
    public static Uri Choose(string? androidValue, string? windowsValue, bool isAndroid)
    {
        var key = isAndroid ? AndroidKey : WindowsKey;
        var value = isAndroid ? androidValue : windowsValue;

        if (!Uri.TryCreate(value, UriKind.Absolute, out var address) || address.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException($"Configuration value '{key}' must be an absolute https:// URL.");
        }

        // HttpClient joins relative paths after the last "/", so the base must end with one.
        return address.AbsoluteUri.EndsWith('/') ? address : new Uri(address.AbsoluteUri + "/");
    }
}
