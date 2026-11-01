namespace ApexPost.Web.Filters;

/// <summary>
/// The real outcome of a request whose HTTP status hides it: a rejected form answers with a redirect or the same
/// page, but the audit log must show that it failed.
/// </summary>
public static class AuditOutcome
{
    private const string OutcomeItem = "ApexPost.AuditOutcome";

    public static void Set(HttpContext context, int status)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Items[OutcomeItem] = status;
    }

    public static int? Of(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.Items[OutcomeItem] as int?;
    }
}
