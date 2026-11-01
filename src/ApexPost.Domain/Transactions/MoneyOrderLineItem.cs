using ApexPost.Domain.Catalogue;
using ApexPost.Domain.Common;

namespace ApexPost.Domain.Transactions;

/// <summary>A money order: the customer pays amount plus fee. Money orders are tax exempt.</summary>
public sealed record MoneyOrderLineItem(Guid Id, Money Amount, Money Fee, string Sender, string Recipient) : ILineItem
{
    public Guid Id { get; } = Guard.NotEmpty(Id, "Line id");

    public Money Amount { get; } = Guard.Required(Amount, nameof(Amount)).IsPositive
        ? Amount
        : throw new DomainException("A money order amount must be greater than zero.");

    public Money Fee { get; } = Guard.Required(Fee, nameof(Fee)).Currency == Amount.Currency
        ? Fee
        : throw new DomainException("The fee must use the money order currency.");

    public string Sender { get; } = Guard.NotBlank(Sender, nameof(Sender), TextLimits.Name);

    public string Recipient { get; } = Guard.NotBlank(Recipient, nameof(Recipient), TextLimits.Name);

    public string Description => $"Money order to {Recipient}";

    public Money Total => Amount + Fee;

    public TaxCategory TaxCategory => TaxCategory.Exempt;
}
