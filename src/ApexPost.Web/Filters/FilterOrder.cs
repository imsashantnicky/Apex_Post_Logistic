namespace ApexPost.Web.Filters;

/// <summary>
/// The order of the global action filters, outermost first: audit also sees what the inner filters reject, and
/// performance measures the action with its checks. Exceptions are turned into results innermost, so that audit
/// reads the outcome of a handled failure.
/// </summary>
public static class FilterOrder
{
    public const int Audit = -1000;
    public const int Performance = -900;
    public const int Validation = -500;
    public const int BranchAccess = -400;
    public const int DomainException = -300;
}
