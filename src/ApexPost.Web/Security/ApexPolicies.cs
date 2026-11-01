namespace ApexPost.Web.Security;

/// <summary>The names of the authorization policies.</summary>
public static class ApexPolicies
{
    public const string BranchManagerOnly = nameof(BranchManagerOnly);
    public const string SameBranchOnly = nameof(SameBranchOnly);
    public const string CanTransact = nameof(CanTransact);
    public const string CanViewReports = nameof(CanViewReports);
    public const string AdministratorOnly = nameof(AdministratorOnly);
    public const string AuditViewers = nameof(AuditViewers);
}
