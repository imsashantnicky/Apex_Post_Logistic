namespace ApexPost.Web.Filters;

/// <summary>The names of the audited events.</summary>
public static class AuditEvents
{
    public const string Login = nameof(Login);
    public const string Logout = nameof(Logout);
    public const string TransactionOpened = nameof(TransactionOpened);
    public const string RetailItemAdded = nameof(RetailItemAdded);
    public const string ShipmentAdded = nameof(ShipmentAdded);
    public const string MoneyOrderAdded = nameof(MoneyOrderAdded);
    public const string IdentityVerification = nameof(IdentityVerification);
    public const string Payment = nameof(Payment);
    public const string Void = nameof(Void);
    public const string Refund = nameof(Refund);
    public const string StockAdjustment = nameof(StockAdjustment);
    public const string StaffCreated = nameof(StaffCreated);
    public const string StaffUpdated = nameof(StaffUpdated);
    public const string StaffDeactivated = nameof(StaffDeactivated);
    public const string StaffActivated = nameof(StaffActivated);
    public const string PasswordChanged = nameof(PasswordChanged);
    public const string BranchClosed = nameof(BranchClosed);
    public const string BranchReopened = nameof(BranchReopened);
    public const string CounterAdded = nameof(CounterAdded);
    public const string CounterRemoved = nameof(CounterRemoved);
    public const string AccessDenied = nameof(AccessDenied);
    public const string AuditViewed = nameof(AuditViewed);
    public const string AuditExported = nameof(AuditExported);
}
