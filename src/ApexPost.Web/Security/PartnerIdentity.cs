namespace ApexPost.Web.Security;

/// <summary>The partner whose API key opened this request.</summary>
public static class PartnerIdentity
{
    private const string PartnerItem = "ApexPost.Partner";

    public static void Set(HttpContext context, string partner)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Items[PartnerItem] = partner;
    }

    public static string? Of(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.Items[PartnerItem] as string;
    }
}
