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
    bool IsManager,
    bool BranchClosed = false)
{
    private const string EmptyBasket = "The basket is empty.";
    private const string OpenHelp = "Build the basket, verify identity if needed, then take payment.";
    private const string DoneHelp = "This transaction is finished; its basket can no longer change.";

    public string EmptyBasketText => CanWork ? $"{EmptyBasket} Add an item or a service below." : EmptyBasket;

    public string Help => Transaction.Status == TransactionStatus.Open ? OpenHelp : DoneHelp;

    public bool CanVoid => IsManager && Transaction.Status is TransactionStatus.Open or TransactionStatus.Paid;

    public bool CanRefund => IsManager && Transaction.Status == TransactionStatus.Paid;

    public bool NeedsVerification => Transaction.RequiresVerification && !Transaction.HasPassedVerification;
}
