/*
    A retail sale as one T-SQL transaction, and what happens when it fails. The application does the same work through
    EF Core and IUnitOfWork; this script shows the mechanics in the open.

    SET XACT_ABORT ON: any run-time error dooms the transaction, so no half-sale can be committed by accident.
    XACT_STATE() in the CATCH block: 1 = open and committable, -1 = doomed, only ROLLBACK is allowed, 0 = none open.
    THROW re-raises the original error, so the caller sees what really failed.

    Run on a performance database; the three calls below leave one paid sale behind.
        sqlcmd -S localhost -E -C -I -d ApexPostDb_Perf -W -s "|" -i sql/transactions/sale-transaction.sql
*/
SET NOCOUNT ON;
GO
CREATE OR ALTER PROCEDURE #Sale
    @BranchCode varchar(5),
    @Sku varchar(8),
    @Quantity int,
    @Method varchar(20)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @Id uniqueidentifier = NEWID();
    DECLARE @Now datetime2 = SYSUTCDATETIME();
    DECLARE @Clerk uniqueidentifier =
    (
        SELECT TOP (1) Id FROM dbo.Staff
        WHERE HomeBranch = @BranchCode AND Role = 'CounterClerk' AND IsActive = 1
        ORDER BY Email
    );
    DECLARE @Counter uniqueidentifier =
        (SELECT TOP (1) Id FROM dbo.Counters WHERE BranchCode = @BranchCode ORDER BY Number);

    BEGIN TRY
        BEGIN TRAN;

        UPDATE dbo.BranchStock
        SET Quantity = Quantity - @Quantity, ModifiedAtUtc = @Now
        WHERE BranchCode = @BranchCode AND Sku = @Sku AND Quantity >= @Quantity;

        IF @@ROWCOUNT = 0
            THROW 51031, 'Sale refused: not enough stock.', 1;

        INSERT dbo.CounterTransactions (Id, BranchCode, CounterId, ClerkId, OpenedAt, Status, CreatedBy)
        VALUES (@Id, @BranchCode, @Counter, @Clerk, @Now, 'Paid', 'sale-example');

        INSERT dbo.TransactionLines (Id, TransactionId, Sku, Name, UnitPrice, Quantity, TaxCategory, CreatedBy)
        SELECT NEWID(), @Id, Sku, Name, UnitPrice, @Quantity, TaxCategory, 'sale-example'
        FROM dbo.Products
        WHERE Sku = @Sku;

        -- The CHECK on Method fails here for an unknown payment method, after the stock has already changed.
        INSERT dbo.Payments (TransactionId, Method, Tax, PaidAt, CreatedBy)
        VALUES (@Id, @Method, 0, @Now, 'sale-example');

        COMMIT TRAN;
    END TRY
    BEGIN CATCH
        DECLARE @State int = XACT_STATE();
        PRINT CONCAT('CATCH: error ', ERROR_NUMBER(), ', XACT_STATE() = ', @State, ', @@TRANCOUNT = ', @@TRANCOUNT);

        IF @State <> 0
            ROLLBACK TRAN;

        THROW;
    END CATCH;
END;
GO
CREATE OR ALTER PROCEDURE #Show @Moment varchar(20)
AS
BEGIN
    DECLARE @Stock int = (SELECT Quantity FROM dbo.BranchStock WHERE BranchCode = 'BR001' AND Sku = 'PKG-0001');
    DECLARE @Sales int = (SELECT COUNT(*) FROM dbo.CounterTransactions WHERE CreatedBy = 'sale-example');
    PRINT CONCAT(@Moment, ': stock ', @Stock, ', example sales ', @Sales);
END;
GO
EXEC #Show @Moment = 'Start';

PRINT 'Call 1: a sale that succeeds';
EXEC #Sale @BranchCode = 'BR001', @Sku = 'PKG-0001', @Quantity = 1, @Method = 'Card';
EXEC #Show @Moment = 'After call 1';

PRINT 'Call 2: not enough stock';
BEGIN TRY
    EXEC #Sale @BranchCode = 'BR001', @Sku = 'PKG-0001', @Quantity = 100000, @Method = 'Card';
END TRY
BEGIN CATCH
    PRINT CONCAT('Caller received: ', ERROR_NUMBER(), ' ', ERROR_MESSAGE());
END CATCH;
EXEC #Show @Moment = 'After call 2';

PRINT 'Call 3: the last INSERT breaks a CHECK constraint, after stock, header and line were written';
BEGIN TRY
    EXEC #Sale @BranchCode = 'BR001', @Sku = 'PKG-0001', @Quantity = 1, @Method = 'Bitcoin';
END TRY
BEGIN CATCH
    PRINT CONCAT('Caller received: ', ERROR_NUMBER(), ' ', ERROR_MESSAGE());
END CATCH;
EXEC #Show @Moment = 'After call 3';
GO
DROP PROCEDURE #Show;
DROP PROCEDURE #Sale;
GO
