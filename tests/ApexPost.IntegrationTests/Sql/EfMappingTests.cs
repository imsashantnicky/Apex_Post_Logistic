using ApexPost.Application.Abstractions;
using ApexPost.Domain.Shipping;
using ApexPost.Domain.Transactions;
using ApexPost.IntegrationTests.Support;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ApexPost.IntegrationTests.Sql;

/// <summary>
/// The EF Core mapping against a real, migrated schema: what the model promises, what a save really stores and what
/// the database refuses by itself when the domain is bypassed.
/// </summary>
public sealed class EfMappingTests(ApexFactory factory) : IClassFixture<ApexFactory>
{
    private const string DocumentNumber = "P12345678";
    private const string ColumnType = """
        SELECT DATA_TYPE + CASE
            WHEN DATA_TYPE = 'decimal' THEN CONCAT('(', NUMERIC_PRECISION, ',', NUMERIC_SCALE, ')')
            WHEN CHARACTER_MAXIMUM_LENGTH IS NOT NULL THEN CONCAT('(', CHARACTER_MAXIMUM_LENGTH, ')')
            ELSE '' END
        FROM INFORMATION_SCHEMA.COLUMNS
        """;

    private const int ConstraintViolation = 547;
    private const int UniqueIndexViolation = 2601;
    private const int PrimaryKeyViolation = 2627;

    private static readonly string[] Tables =
    [
        "AuditEntries", "BranchStock", "Branches", "CounterTransactions", "Counters", "IdentityVerifications",
        "MoneyOrders", "Payments", "ProductCategories", "Products", "Roles", "Shipments", "ShippingRates",
        "ShippingZones", "Staff", "StockAdjustments", "TransactionLines",
    ];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_model_and_the_migrations_agree()
    {
        await using var database = factory.OpenDatabase();

        Assert.False(database.Database.HasPendingModelChanges());
        Assert.Empty(await database.Database.GetPendingMigrationsAsync(Ct));
    }

    [Fact]
    public async Task All_17_tables_exist_and_each_has_the_audit_columns()
    {
        var withAudit = await factory.ColumnAsync("""
            SELECT TABLE_NAME FROM INFORMATION_SCHEMA.COLUMNS
            WHERE COLUMN_NAME IN ('CreatedAtUtc', 'CreatedBy', 'ModifiedAtUtc')
            GROUP BY TABLE_NAME HAVING COUNT(*) = 3
            ORDER BY TABLE_NAME COLLATE Latin1_General_BIN2
            """);

        Assert.Equal(Tables, withAudit);
    }

    [Fact]
    public async Task Only_stock_and_transactions_carry_a_row_version()
    {
        var rows = await factory.ColumnAsync("""
            SELECT TABLE_NAME FROM INFORMATION_SCHEMA.COLUMNS
            WHERE COLUMN_NAME = 'RowVersion' AND DATA_TYPE = 'timestamp'
            ORDER BY TABLE_NAME
            """);

        Assert.Equal(["BranchStock", "CounterTransactions"], rows);
    }

    [Theory]
    [InlineData("Products", "UnitPrice", "decimal(18,2)")]
    [InlineData("Payments", "Tax", "decimal(18,2)")]
    [InlineData("MoneyOrders", "Amount", "decimal(18,2)")]
    [InlineData("Shipments", "Price", "decimal(18,2)")]
    [InlineData("CounterTransactions", "OpenedAt", "datetime2")]
    [InlineData("CounterTransactions", "Status", "varchar(20)")]
    [InlineData("Branches", "Code", "varchar(5)")]
    [InlineData("Products", "Sku", "varchar(8)")]
    [InlineData("Staff", "Email", "varchar(254)")]
    [InlineData("Staff", "Name", "nvarchar(100)")]
    [InlineData("IdentityVerifications", "DocumentLast4", "varchar(4)")]
    public async Task Columns_have_the_agreed_types(string table, string column, string expected)
    {
        var actual = await factory.ScalarAsync<string>(
            $"{ColumnType} WHERE TABLE_NAME = '{table}' AND COLUMN_NAME = '{column}'");

        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task A_counter_cannot_exist_without_its_branch()
    {
        var nullable = await factory.ScalarAsync<string>("""
            SELECT IS_NULLABLE FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_NAME = 'Counters' AND COLUMN_NAME = 'BranchCode'
            """);

        Assert.Equal("NO", nullable);
    }

    [Fact]
    public async Task Status_defaults_to_open_and_times_default_to_utc_in_the_database()
    {
        var defaults = await factory.ColumnAsync("""
            SELECT COLUMN_DEFAULT FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_NAME = 'CounterTransactions' AND COLUMN_NAME IN ('CreatedAtUtc', 'Status')
            ORDER BY COLUMN_NAME
            """);

        Assert.Equal(["(sysutcdatetime())", "('Open')"], defaults);
    }

    [Fact]
    public async Task A_mixed_basket_comes_back_from_the_database_as_the_same_aggregate()
    {
        var counter = await PaidMixedBasketAsync();
        await using var scope = factory.Services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<ICounterTransactionRepository>();

        var stored = await repository.GetAsync(Guid.Parse(counter.Id), Ct);

        Assert.NotNull(stored);
        Assert.Equal(TransactionStatus.Paid, stored.Status);
        Assert.Equal(3, stored.Lines.Count);
        var retail = Assert.Single(stored.Lines.OfType<RetailLineItem>());
        Assert.Equal(2, retail.Quantity);
        var parcel = Assert.Single(stored.Lines.OfType<ShippingLineItem>());
        Assert.Equal(ServiceLevel.Express, parcel.Shipment.Level);
        Assert.Equal(new Weight(2000), parcel.Shipment.Weight);
        Assert.Equal(new Dimensions(30m, 20m, 10m), parcel.Shipment.Dimensions);
        Assert.Equal("Hannah Reid", parcel.Shipment.Recipient.Name);
        Assert.Equal("BS1 4DJ", parcel.Shipment.Recipient.PostalCode);
        var order = Assert.Single(stored.Lines.OfType<MoneyOrderLineItem>());
        Assert.Equal(350m, order.Amount.Amount);
        Assert.Equal(7m, order.Fee.Amount);
        var check = Assert.Single(stored.Verifications);
        Assert.Equal("****5678", check.MaskedNumber);
        Assert.Equal(PaymentMethod.Card, stored.Payment?.Method);
        Assert.Equal(TimeSpan.Zero, stored.OpenedAt.Offset);
    }

    [Fact]
    public async Task Stored_values_are_what_the_schema_promises_and_the_full_number_is_nowhere()
    {
        var counter = await PaidMixedBasketAsync();
        var id = counter.Id;

        var status = await factory.ScalarAsync<string>($"SELECT Status FROM CounterTransactions WHERE Id = '{id}'");
        var last4 = await factory.ScalarAsync<string>(
            $"SELECT DocumentLast4 FROM IdentityVerifications WHERE TransactionId = '{id}'");
        var grams = await factory.ScalarAsync<int>($"SELECT WeightGrams FROM Shipments WHERE TransactionId = '{id}'");
        var method = await factory.ScalarAsync<string>($"SELECT Method FROM Payments WHERE TransactionId = '{id}'");
        var leaks = await factory.ScalarAsync<int>($"""
            SELECT (SELECT COUNT(*) FROM AuditEntries WHERE RouteValues LIKE '%{DocumentNumber}%')
                 + (SELECT COUNT(*) FROM IdentityVerifications WHERE DocumentLast4 LIKE '%1234%')
            """);

        Assert.Equal("Paid", status);
        Assert.Equal("5678", last4);
        Assert.Equal(2000, grams);
        Assert.Equal("Card", method);
        Assert.Equal(0, leaks);
    }

    [Fact]
    public async Task The_interceptor_stamps_who_created_a_row_and_when_it_changed()
    {
        var counter = await PaidMixedBasketAsync();
        var id = counter.Id;
        var clerk = await factory.StaffAsync(DemoAccounts.Clerk);

        var createdBy = await factory.ScalarAsync<string>(
            $"SELECT CreatedBy FROM CounterTransactions WHERE Id = '{id}'");
        var modified = await factory.ScalarAsync<int>(
            $"SELECT COUNT(*) FROM CounterTransactions WHERE Id = '{id}' AND ModifiedAtUtc IS NOT NULL");

        Assert.Equal(clerk.Id.ToString(), createdBy, ignoreCase: true);
        Assert.Equal(1, modified);
    }

    [Fact]
    public async Task A_save_moves_the_row_version_of_the_transaction()
    {
        using var clerk = await CounterBrowser.SignInAsync(factory, DemoAccounts.Clerk);
        var counter = await Counter.OpenAsync(clerk);
        var query = $"SELECT CONVERT(varchar(18), RowVersion, 1) FROM CounterTransactions WHERE Id = '{counter.Id}'";
        var before = await factory.ScalarAsync<string>(query);

        using var line = await counter.AddRetailAsync("PKG-0002");

        Assert.NotEqual(before, await factory.ScalarAsync<string>(query));
    }

    [Theory]
    [InlineData("UPDATE TOP (1) BranchStock SET Quantity = -1")]
    [InlineData("UPDATE TOP (1) Products SET UnitPrice = 0")]
    [InlineData("UPDATE TOP (1) Products SET TaxCategory = 'Luxury'")]
    [InlineData("UPDATE TOP (1) CounterTransactions SET Status = 'Lost'")]
    [InlineData("UPDATE TOP (1) Branches SET OpensAt = '20:00', ClosesAt = '08:00'")]
    [InlineData("UPDATE TOP (1) Staff SET Role = 'Janitor'")]
    [InlineData("UPDATE TOP (1) Counters SET Number = 0")]
    public async Task The_database_refuses_a_value_the_domain_would_never_send(string statement)
    {
        var refused = await Assert.ThrowsAsync<SqlException>(() => factory.ExecuteAsync(statement));

        Assert.Equal(ConstraintViolation, refused.Number);
    }

    [Fact]
    public async Task The_database_refuses_a_full_document_number_or_an_invented_detail_row()
    {
        var paid = await PaidMixedBasketAsync();

        var tooLong = await Assert.ThrowsAsync<SqlException>(() => factory.ExecuteAsync(
            $"UPDATE IdentityVerifications SET DocumentLast4 = '123' WHERE TransactionId = '{paid.Id}'"));
        var overweight = await Assert.ThrowsAsync<SqlException>(() => factory.ExecuteAsync(
            $"UPDATE Shipments SET WeightGrams = 30001 WHERE TransactionId = '{paid.Id}'"));
        var noAmount = await Assert.ThrowsAsync<SqlException>(() => factory.ExecuteAsync(
            $"UPDATE MoneyOrders SET Amount = 0 WHERE TransactionId = '{paid.Id}'"));

        Assert.All([tooLong, overweight, noAmount], error => Assert.Equal(ConstraintViolation, error.Number));
    }

    [Fact]
    public async Task Emails_and_primary_keys_stay_unique()
    {
        var email = await Assert.ThrowsAsync<SqlException>(() => factory.ExecuteAsync($"""
            INSERT INTO Staff (Id, Name, Email, Role, HomeBranch)
            VALUES (NEWID(), 'Copy', '{DemoAccounts.Clerk}', 'CounterClerk', 'BR001')
            """));
        var sku = await Assert.ThrowsAsync<SqlException>(() => factory.ExecuteAsync("""
            INSERT INTO Products (Sku, Name, Category, UnitPrice, TaxCategory)
            VALUES ('PKG-0001', 'Copy', 'Packaging', 1, 'Standard')
            """));
        var branch = await Assert.ThrowsAsync<SqlException>(() => factory.ExecuteAsync("""
            INSERT INTO Branches (Code, Name, Region, OpensAt, ClosesAt)
            VALUES ('BR001', 'Copy', 'North', '08:00', '18:00')
            """));

        Assert.Equal(UniqueIndexViolation, email.Number);
        Assert.All([sku, branch], error => Assert.Equal(PrimaryKeyViolation, error.Number));
    }

    [Theory]
    [InlineData("DELETE FROM Branches WHERE Code = 'BR001'")]
    [InlineData("DELETE FROM Products WHERE Sku IN (SELECT TOP (1) Sku FROM TransactionLines)")]
    [InlineData("DELETE FROM Staff WHERE Id IN (SELECT TOP (1) ClerkId FROM CounterTransactions)")]
    [InlineData("DELETE FROM CounterTransactions WHERE Id IN (SELECT TOP (1) TransactionId FROM IdentityVerifications)")]
    public async Task History_is_never_deleted_by_a_side_effect(string statement)
    {
        await PaidMixedBasketAsync();

        var refused = await Assert.ThrowsAsync<SqlException>(() => factory.ExecuteAsync(statement));

        Assert.Equal(ConstraintViolation, refused.Number);
    }

    [Fact]
    public async Task Deleting_a_transaction_takes_only_its_own_lines_and_payment()
    {
        using var clerk = await CounterBrowser.SignInAsync(factory, DemoAccounts.Clerk);
        var counter = await Counter.OpenAsync(clerk);
        using var retail = await counter.AddRetailAsync("PKG-0003");
        using var parcel = await counter.AddParcelAsync();
        using var paid = await counter.PayAsync();
        var transactionsBefore = await factory.ScalarAsync<int>("SELECT COUNT(*) FROM CounterTransactions");

        await factory.ExecuteAsync($"DELETE FROM CounterTransactions WHERE Id = '{counter.Id}'");

        var left = await factory.ScalarAsync<int>($"""
            SELECT (SELECT COUNT(*) FROM TransactionLines WHERE TransactionId = '{counter.Id}')
                 + (SELECT COUNT(*) FROM Shipments WHERE TransactionId = '{counter.Id}')
                 + (SELECT COUNT(*) FROM Payments WHERE TransactionId = '{counter.Id}')
            """);
        Assert.Equal(0, left);
        Assert.Equal(
            transactionsBefore - 1,
            await factory.ScalarAsync<int>("SELECT COUNT(*) FROM CounterTransactions"));
    }

    [Fact]
    public async Task Every_foreign_key_restricts_except_the_owned_children()
    {
        var cascades = await factory.ColumnAsync("""
            SELECT OBJECT_NAME(parent_object_id) FROM sys.foreign_keys
            WHERE delete_referential_action_desc = 'CASCADE'
            ORDER BY 1
            """);
        var restricted = await factory.ScalarAsync<int>(
            "SELECT COUNT(*) FROM sys.foreign_keys WHERE delete_referential_action_desc = 'NO_ACTION'");

        Assert.Equal(["MoneyOrders", "Payments", "Shipments", "ShippingRates", "TransactionLines"], cascades);
        Assert.Equal(16, restricted);
    }

    /// <summary>The fixture is shared, so each sale takes one unit of stock that the others leave alone.</summary>
    private async Task<Counter> PaidMixedBasketAsync()
    {
        using var clerk = await CounterBrowser.SignInAsync(factory, DemoAccounts.Clerk);
        var counter = await Counter.OpenAsync(clerk);
        using var retail = await counter.AddRetailAsync("STA-0004", 2);
        using var parcel = await counter.AddParcelAsync(2m, "Express");
        using var order = await counter.AddMoneyOrderAsync(350m);
        using var check = await counter.VerifyAsync(DocumentNumber);
        await factory.SetQuantityAsync("STA-0004", 50);
        using var paid = await counter.PayAsync();
        return counter;
    }
}
