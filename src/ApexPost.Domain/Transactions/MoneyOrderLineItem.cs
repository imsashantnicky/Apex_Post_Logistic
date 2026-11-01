using ApexPost.Domain.Catalogue;
using ApexPost.Domain.Common;
using ApexPost.Domain.Rules;

namespace ApexPost.Domain.Transactions;

/// <summary>A money order: the customer pays amount plus fee. Money orders are tax exempt.</summary>
public sealed record MoneyOrderLineItem : TransactionLine
{
    public MoneyOrderLineItem(Guid id, Money amount, Money fee, string sender, string recipient)
        : base(id)
    {
        Amount = Guard.Required(amount, nameof(Amount)).IsPositive
            ? amount
            : throw new DomainException("A money order amount must be greater than zero.");
        Fee = Guard.Required(fee, nameof(Fee)).Currency == amount.Currency
            ? fee
            : throw new DomainException("The fee must use the money order currency.");
        Sender = Guard.NotBlank(sender, nameof(Sender), TextLimits.Name);
        Recipient = Guard.NotBlank(recipient, nameof(Recipient), TextLimits.Name);
    }

    public Money Amount { get; }

    public Money Fee { get; }

    public string Sender { get; }

    public string Recipient { get; }

    public override string Description => $"Money order to {Recipient}";

    public override Money Total => Amount + Fee;

    public override TaxCategory TaxCategory => TaxCategory.Exempt;

    public static MoneyOrderLineItem Issue(Money amount, string sender, string recipient) =>
        new(Guid.CreateVersion7(), amount, MoneyOrderFeeRule.FeeFor(amount), sender, recipient);
}
