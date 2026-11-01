namespace ApexPost.Web.Composition;

/// <summary>The conventional routes of the application and the paths the pipeline treats specially.</summary>
public static class ApexRoutes
{
    public const string DefaultName = "default";
    public const string DefaultPattern = "{controller=Dashboard}/{action=Index}/{id?}";
    public const string LoginPath = "/Account/Login";

    public const string ParcelStatusName = "integration-parcel-status";
    public const string ParcelStatusPattern = "integration/parcel-status/{trackingNumber}";
    public const string IntegrationPath = "/integration";

    public const string HealthPath = "/health";
    public const string StatusPagePattern = "/Error/Status/{0}";
    public const string ExceptionPagePath = "/Error/Status/500";

    public static readonly object ParcelStatusTarget = new { controller = "Integration", action = "ParcelStatus" };

    public static bool IsIntegration(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.Request.Path.StartsWithSegments(IntegrationPath, StringComparison.OrdinalIgnoreCase);
    }
}
