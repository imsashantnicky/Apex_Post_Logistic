/*
    Daily branch sales, split into retail and services.
    Parameters: @From, @To (inclusive days), @BranchCode (NULL = all), @MinTransactions (hide quiet days).
    Sent with sp_executesql by sql/perf/Measure-Workloads.ps1; the DECLARE block is for running it by hand.

    Each amount is added up per transaction first and then joined once; a first version with OUTER APPLY per
    transaction cost far more (PERFORMANCE.md, R1).
*/
DECLARE @From date = DATEADD(DAY, -14, CAST(SYSUTCDATETIME() AS date));
DECLARE @To date = CAST(SYSUTCDATETIME() AS date);
DECLARE @BranchCode varchar(5) = NULL;
DECLARE @MinTransactions int = 1;
-- query
WITH Paid AS
(
    SELECT tx.Id, tx.BranchCode, CAST(payment.PaidAt AS date) AS SalesDay, payment.Tax
    FROM dbo.Payments AS payment
    INNER JOIN dbo.CounterTransactions AS tx ON tx.Id = payment.TransactionId
    WHERE payment.PaidAt >= CAST(@From AS datetime2)
      AND payment.PaidAt < DATEADD(DAY, 1, CAST(@To AS datetime2))
      AND tx.Status = 'Paid'
      AND (@BranchCode IS NULL OR tx.BranchCode = @BranchCode)
),
Retail AS
(
    SELECT line.TransactionId, SUM(line.UnitPrice * line.Quantity) AS Amount
    FROM dbo.TransactionLines AS line
    INNER JOIN Paid AS paid ON paid.Id = line.TransactionId
    GROUP BY line.TransactionId
),
Shipping AS
(
    SELECT shipment.TransactionId, SUM(shipment.Price) AS Amount
    FROM dbo.Shipments AS shipment
    INNER JOIN Paid AS paid ON paid.Id = shipment.TransactionId
    GROUP BY shipment.TransactionId
),
Orders AS
(
    SELECT item.TransactionId, SUM(item.Amount + item.Fee) AS Amount
    FROM dbo.MoneyOrders AS item
    INNER JOIN Paid AS paid ON paid.Id = item.TransactionId
    GROUP BY item.TransactionId
)
SELECT paid.SalesDay,
       branch.Code AS BranchCode,
       branch.Name AS BranchName,
       COUNT(*) AS Transactions,
       SUM(COALESCE(retail.Amount, 0)) AS Retail,
       SUM(COALESCE(shipping.Amount, 0) + COALESCE(orders.Amount, 0)) AS Services,
       SUM(paid.Tax) AS Tax
FROM Paid AS paid
INNER JOIN dbo.Branches AS branch ON branch.Code = paid.BranchCode
LEFT JOIN Retail AS retail ON retail.TransactionId = paid.Id
LEFT JOIN Shipping AS shipping ON shipping.TransactionId = paid.Id
LEFT JOIN Orders AS orders ON orders.TransactionId = paid.Id
GROUP BY paid.SalesDay, branch.Code, branch.Name
HAVING COUNT(*) >= @MinTransactions
ORDER BY paid.SalesDay, branch.Code;
