/*
    Paid sales per UTC day and branch, split into retail and services. Backs /Reports/DailySales and the dashboard.

    @From and @To are inclusive days; @BranchCode NULL means all branches. Only transactions still Paid count, so a
    void or a refund takes its sale out of the report. Why this is a procedure and not LINQ: ADR-012 in DESIGN.md.

    The range stays on the PaidAt column and is built from the parameters inside the predicate, so the optimizer sees
    the sniffed days; a local variable would hide them (PERFORMANCE.md, estimate investigation).
*/
CREATE OR ALTER PROCEDURE dbo.usp_DailyBranchSales
    @From date,
    @To date,
    @BranchCode varchar(5) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF @From IS NULL OR @To IS NULL
        THROW 51001, 'usp_DailyBranchSales: @From and @To are required.', 1;

    IF @To < @From
        THROW 51002, 'usp_DailyBranchSales: @To must not be before @From.', 1;

    IF @BranchCode IS NOT NULL AND NOT EXISTS (SELECT 1 FROM dbo.Branches WHERE Code = @BranchCode)
        THROW 51003, 'usp_DailyBranchSales: the branch does not exist.', 1;

    -- The period is fixed first: the amounts below then join real rows with real statistics, whatever the range.
    CREATE TABLE #Paid
    (
        Id uniqueidentifier NOT NULL PRIMARY KEY,
        BranchCode varchar(5) NOT NULL,
        SalesDay date NOT NULL,
        Tax decimal(18, 2) NOT NULL
    );

    INSERT #Paid (Id, BranchCode, SalesDay, Tax)
    SELECT tx.Id, tx.BranchCode, CAST(payment.PaidAt AS date), payment.Tax
    FROM dbo.Payments AS payment
    INNER JOIN dbo.CounterTransactions AS tx ON tx.Id = payment.TransactionId
    WHERE payment.PaidAt >= CAST(@From AS datetime2)
      AND payment.PaidAt < DATEADD(DAY, 1, CAST(@To AS datetime2))
      AND tx.Status = 'Paid'
      AND (@BranchCode IS NULL OR tx.BranchCode = @BranchCode);

    WITH Amounts AS
    (
        SELECT line.TransactionId, line.UnitPrice * line.Quantity AS Retail, CAST(0 AS decimal(18, 2)) AS Services
        FROM dbo.TransactionLines AS line
        INNER JOIN #Paid AS paid ON paid.Id = line.TransactionId
        UNION ALL
        SELECT shipment.TransactionId, 0, shipment.Price
        FROM dbo.Shipments AS shipment
        INNER JOIN #Paid AS paid ON paid.Id = shipment.TransactionId
        UNION ALL
        SELECT moneyOrder.TransactionId, 0, moneyOrder.Amount + moneyOrder.Fee
        FROM dbo.MoneyOrders AS moneyOrder
        INNER JOIN #Paid AS paid ON paid.Id = moneyOrder.TransactionId
    ),
    Basket AS
    (
        SELECT TransactionId, SUM(Retail) AS Retail, SUM(Services) AS Services
        FROM Amounts
        GROUP BY TransactionId
    )
    SELECT paid.SalesDay,
           paid.BranchCode,
           COUNT(*) AS Transactions,
           CAST(COALESCE(SUM(basket.Retail), 0) AS decimal(18, 2)) AS Retail,
           CAST(COALESCE(SUM(basket.Services), 0) AS decimal(18, 2)) AS Services,
           CAST(SUM(paid.Tax) AS decimal(18, 2)) AS Tax
    FROM #Paid AS paid
    LEFT JOIN Basket AS basket ON basket.TransactionId = paid.Id
    GROUP BY paid.SalesDay, paid.BranchCode
    ORDER BY paid.SalesDay, paid.BranchCode;
END;
