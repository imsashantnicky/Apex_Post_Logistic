using ApexPost.Application.Extensions;
using ApexPost.Domain.Branches;
using ApexPost.Domain.Common;
using ApexPost.Domain.Transactions;

namespace ApexPost.Application.DTOs;

/// <summary>
/// The counter screen of one transaction. While the basket is open, Tax and Total are a preview with the
/// current tax rates; after payment they are the frozen values.
/// </summary>
public sealed record TransactionDetails(
    Guid Id,
    BranchCode Branch,
    DateTimeOffset OpenedAt,
    TransactionStatus Status,
    IReadOnlyList<LineView> Lines,
    Money RetailTotal,
    Money ServicesTotal,
    Money Subtotal,
    Money Tax,
    Money Total,
    Money AmountDue,
    PaymentMethod? PaymentMethod,
    bool RequiresVerification,
    string? Verification,
    string? ReversalReason,
    int Version)
{
    public static TransactionDetails From(
        CounterTransaction transaction,
        Func<ILineItem, Money> taxRule,
        Money verificationThreshold)
    {
        var tax = transaction.PreviewTax(taxRule);
        var total = transaction.Subtotal + tax;
        return new(
            transaction.Id,
            transaction.Branch,
            transaction.OpenedAt,
            transaction.Status,
            [.. transaction.Lines.Select(LineView.From)],
            transaction.RetailTotal,
            transaction.ServicesTotal,
            transaction.Subtotal,
            tax,
            total,
            transaction.Payment is null ? total : transaction.AmountDue,
            transaction.Payment?.Method,
            transaction.RequiresVerification(verificationThreshold),
            transaction.VerificationSummary,
            transaction.Reversal?.Reason,
            transaction.Version);
    }
}
