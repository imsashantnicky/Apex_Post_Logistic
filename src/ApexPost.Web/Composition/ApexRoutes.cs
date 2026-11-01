namespace ApexPost.Web.Composition;

/// <summary>The conventional routes of the application.</summary>
public static class ApexRoutes
{
    public const string DefaultName = "default";
    public const string DefaultPattern = "{controller=Dashboard}/{action=Index}/{id?}";
    public const string LoginPath = "/Account/Login";
}
