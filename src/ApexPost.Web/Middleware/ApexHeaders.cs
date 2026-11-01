namespace ApexPost.Web.Middleware;

/// <summary>The custom HTTP headers of the platform.</summary>
public static class ApexHeaders
{
    public const string CorrelationId = "X-Correlation-Id";
    public const string ResponseTime = "X-Response-Time-Ms";
    public const string ApiKey = "X-Api-Key";
}
