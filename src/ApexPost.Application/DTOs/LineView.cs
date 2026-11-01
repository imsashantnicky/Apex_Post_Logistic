using ApexPost.Domain.Common;
using ApexPost.Domain.Transactions;

namespace ApexPost.Application.DTOs;

/// <summary>One basket line as shown on the counter screen.</summary>
public sealed record LineView(Guid Id, LineKind Kind, string Description, int Quantity, Money UnitPrice, Money Total)
{
    public static LineView From(ILineItem line) => line switch
    {
        RetailLineItem retail => new(retail.Id, LineKind.Retail, retail.Description, retail.Quantity,
            retail.UnitPrice, retail.Total),
        ShippingLineItem shipping => new(shipping.Id, LineKind.Shipping, shipping.Description, 1,
            shipping.Total, shipping.Total),
        MoneyOrderLineItem order => new(order.Id, LineKind.MoneyOrder, order.Description, 1,
            order.Total, order.Total),
        _ => throw new ArgumentOutOfRangeException(nameof(line), line.GetType().Name, "Unknown line type."),
    };
}
