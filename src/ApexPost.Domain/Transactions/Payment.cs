using ApexPost.Domain.Common;

namespace ApexPost.Domain.Transactions;

/// <summary>How and when a basket was paid, with the tax frozen at that moment.</summary>
public sealed record Payment(PaymentMethod Method, Money Tax, DateTimeOffset PaidAt)
{
    public PaymentMethod Method { get; } = Guard.Defined(Method, nameof(Method));

    public Money Tax { get; } = Guard.Required(Tax, nameof(Tax));
}
