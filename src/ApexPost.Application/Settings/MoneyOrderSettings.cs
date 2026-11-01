namespace ApexPost.Application.Settings;

/// <summary>The "MoneyOrders" section: when identity verification is needed and when an alert is raised.</summary>
public sealed class MoneyOrderSettings
{
    public const string SectionName = "MoneyOrders";

    /// <summary>Amounts above this value need a passed identity verification; exactly this value does not.</summary>
    public decimal IdentityVerificationThreshold { get; set; } = 300m;

    /// <summary>Amounts above this value raise a high-value notification.</summary>
    public decimal HighValueAlertAmount { get; set; } = 1000m;
}
