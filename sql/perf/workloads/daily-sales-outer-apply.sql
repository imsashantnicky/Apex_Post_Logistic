-- The first version of sql/reports/01-daily-branch-sales.sql, kept to reproduce its cost: OUTER APPLY per transaction.
WITH Baskets AS
(
    SELECT tx.Id, tx.BranchCode, CAST(payment.PaidAt AS date) AS SalesDay, payment.Tax,
           retail.Amount AS Retail, shipping.Amount AS Shipping, moneyOrder.Amount AS MoneyOrders
    FROM dbo.Payments AS payment
    INNER JOIN dbo.CounterTransactions AS tx ON tx.Id = payment.TransactionId
    OUTER APPLY
    (
        SELECT SUM(line.UnitPrice * line.Quantity) AS Amount
        FROM dbo.TransactionLines AS line
        WHERE line.TransactionId = tx.Id
    ) AS retail
    OUTER APPLY
    (
        SELECT SUM(shipment.Price) AS Amount
        FROM dbo.Shipments AS shipment
        WHERE shipment.TransactionId = tx.Id
    ) AS shipping
    OUTER APPLY
    (
        SELECT SUM(item.Amount + item.Fee) AS Amount
        FROM dbo.MoneyOrders AS item
        WHERE item.TransactionId = tx.Id
    ) AS moneyOrder
    WHERE payment.PaidAt >= CAST(@From AS datetime2)
      AND payment.PaidAt < DATEADD(DAY, 1, CAST(@To AS datetime2))
      AND tx.Status = 'Paid'
      AND (@BranchCode IS NULL OR tx.BranchCode = @BranchCode)
)
SELECT baskets.SalesDay, branch.Code AS BranchCode, branch.Name AS BranchName, COUNT(*) AS Transactions,
       SUM(COALESCE(baskets.Retail, 0)) AS Retail,
       SUM(COALESCE(baskets.Shipping, 0) + COALESCE(baskets.MoneyOrders, 0)) AS Services,
       SUM(baskets.Tax) AS Tax
FROM Baskets AS baskets
INNER JOIN dbo.Branches AS branch ON branch.Code = baskets.BranchCode
GROUP BY baskets.SalesDay, branch.Code, branch.Name
HAVING COUNT(*) >= @MinTransactions
ORDER BY baskets.SalesDay, branch.Code;
