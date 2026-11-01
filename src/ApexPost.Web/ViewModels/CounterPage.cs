using ApexPost.Application.DTOs;
using ApexPost.Domain.Common;
using ApexPost.Domain.Shipping;
using ApexPost.Domain.Transactions;

namespace ApexPost.Web.ViewModels;

/// <summary>The counter screen: the basket, and what the user may do with it now.</summary>
public sealed record CounterPage(
    TransactionDetails Transaction,
    IReadOnlyList<CatalogueItem> Products,
    IReadOnlyList<ShippingZone> Zones,
    Money VerificationThreshold,
    bool CanWork,
    bool IsManager)
{
    public bool CanVoid => IsManager && Transaction.Status is TransactionStatus.Open or TransactionStatus.Paid;

    public bool CanRefund => IsManager && Transaction.Status == TransactionStatus.Paid;

    public bool NeedsVerification => Transaction.RequiresVerification && !Transaction.HasPassedVerification;
}
