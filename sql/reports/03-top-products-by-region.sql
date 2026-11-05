/*
    Top products by retail revenue per region, over a period. Ties share a rank, so a region can show more than @Top
    rows. LEFT JOIN keeps the sold name of a line whose product has since been renamed or removed from sale.
    OPTION (RECOMPILE): the period decides the plan, and a cached 30-day plan spilled to tempdb on a one-year run. The
    report is rare, so a compile on each run is cheap next to that (PERFORMANCE.md, estimate investigation).
    Parameters: @From, @To (inclusive days), @Top.
*/
DECLARE @From date = DATEADD(DAY, -30, CAST(SYSUTCDATETIME() AS date));
DECLARE @To date = CAST(SYSUTCDATETIME() AS date);
DECLARE @Top int = 10;
-- query
WITH Revenue AS
(
    SELECT branch.Region,
           line.Sku,
           COALESCE(product.Name, MAX(line.Name)) AS Product,
           SUM(line.Quantity) AS Units,
           SUM(line.UnitPrice * line.Quantity) AS Revenue
    FROM dbo.Payments AS payment
    INNER JOIN dbo.CounterTransactions AS tx ON tx.Id = payment.TransactionId
    INNER JOIN dbo.Branches AS branch ON branch.Code = tx.BranchCode
    INNER JOIN dbo.TransactionLines AS line ON line.TransactionId = tx.Id
    LEFT JOIN dbo.Products AS product ON product.Sku = line.Sku
    WHERE payment.PaidAt >= CAST(@From AS datetime2)
      AND payment.PaidAt < DATEADD(DAY, 1, CAST(@To AS datetime2))
      AND tx.Status = 'Paid'
    GROUP BY branch.Region, line.Sku, product.Name
    HAVING SUM(line.UnitPrice * line.Quantity) > 0
),
Ranked AS
(
    SELECT Region, Sku, Product, Units, Revenue,
           RANK() OVER (PARTITION BY Region ORDER BY Revenue DESC) AS RevenueRank
    FROM Revenue
)
SELECT Region, RevenueRank, Sku, Product, Units, Revenue
FROM Ranked
WHERE RevenueRank <= @Top
ORDER BY Region, RevenueRank, Sku
OPTION (RECOMPILE);
