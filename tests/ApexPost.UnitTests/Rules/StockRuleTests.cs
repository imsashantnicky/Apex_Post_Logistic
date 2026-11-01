using ApexPost.Domain.Common;
using ApexPost.Domain.Inventory;
using ApexPost.Domain.Personnel;
using ApexPost.Domain.Transactions;
using static ApexPost.UnitTests.Support.TestData;

namespace ApexPost.UnitTests.Rules;

/// <summary>Rule 6: branch stock never goes negative; a short line stops the whole payment.</summary>
public sealed class StockRuleTests
{
    [Fact]
    public void Sale_reduces_branch_stock()
    {
        var stock = CreateStock(FirstSku, 10);

        Pay(stock, (FirstSku, 3));

        Assert.Equal(7, stock.Quantity);
    }

    [Fact]
    public void Selling_exactly_the_available_quantity_leaves_zero()
    {
        var stock = CreateStock(FirstSku, 3);

        Pay(stock, (FirstSku, 3));

        Assert.Equal(0, stock.Quantity);
    }

    [Fact]
    public void One_short_line_fails_the_payment_and_changes_nothing()
    {
        var first = CreateStock(FirstSku, 5);
        var second = CreateStock(SecondSku, 1);
        var (transaction, clerk) = Basket((FirstSku, 2), (SecondSku, 2));

        Assert.Throws<InsufficientStockException>(() =>
            transaction.Pay(clerk, PaymentMethod.Card, CreateRules(), [first, second], Now));

        Assert.Equal(5, first.Quantity);
        Assert.Equal(1, second.Quantity);
        Assert.Equal(TransactionStatus.Open, transaction.Status);
        Assert.Null(transaction.Payment);
        Assert.Empty(transaction.TakeEvents());
    }

    [Fact]
    public void Lines_of_the_same_product_are_added_up_before_the_check()
    {
        var stock = CreateStock(FirstSku, 3);

        Assert.Throws<InsufficientStockException>(() => Pay(stock, (FirstSku, 2), (FirstSku, 2)));
        Assert.Equal(3, stock.Quantity);
    }

    [Fact]
    public void Product_without_stock_at_the_branch_cannot_be_sold()
    {
        var otherBranchStock = CreateStock(FirstSku, 10, OtherBranch);

        Assert.Throws<InsufficientStockException>(() => Pay(otherBranchStock, (FirstSku, 1)));
        Assert.Equal(10, otherBranchStock.Quantity);
    }

    [Fact]
    public void Stock_item_cannot_start_below_zero()
    {
        Assert.Equal(0, CreateStock(FirstSku, 0).Quantity);
        Assert.Throws<DomainException>(() => CreateStock(FirstSku, -1));
    }

    [Fact]
    public void Removing_more_than_available_is_rejected_and_keeps_the_quantity()
    {
        var stock = CreateStock(FirstSku, 2);

        Assert.Throws<InsufficientStockException>(() => stock.Remove(3));
        Assert.Equal(2, stock.Quantity);
    }

    [Fact]
    public void Adding_and_removing_need_a_positive_quantity()
    {
        var stock = CreateStock(FirstSku, 2);

        Assert.Throws<DomainException>(() => stock.Add(0));
        Assert.Throws<DomainException>(() => stock.Remove(0));
        stock.Add(3);
        Assert.Equal(5, stock.Quantity);
    }

    private static void Pay(StockItem stock, params (string Sku, int Quantity)[] lines)
    {
        var (transaction, clerk) = Basket(lines);
        transaction.Pay(clerk, PaymentMethod.Card, CreateRules(), [stock], Now);
    }

    private static (CounterTransaction Transaction, Staff Clerk) Basket(params (string Sku, int Quantity)[] lines)
    {
        var (transaction, clerk) = OpenBasket();
        foreach (var (sku, quantity) in lines)
        {
            transaction.AddLine(clerk, RetailLineItem.For(CreateProduct(sku), quantity));
        }

        return (transaction, clerk);
    }
}
