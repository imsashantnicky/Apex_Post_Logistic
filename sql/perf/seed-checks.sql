/*
    Checks the volume data on a performance database: row counts, the mix a report depends on, and the domain rules.
    Every violation count must be 0.
        sqlcmd -S localhost -E -C -d ApexPostDb_Perf -W -s "|" -i sql/perf/seed-checks.sql
*/
SET NOCOUNT ON;

SELECT 'CounterTransactions' AS TableName, COUNT(*) AS RowTotal FROM dbo.CounterTransactions
UNION ALL SELECT 'TransactionLines', COUNT(*) FROM dbo.TransactionLines
UNION ALL SELECT 'Shipments', COUNT(*) FROM dbo.Shipments
UNION ALL SELECT 'MoneyOrders', COUNT(*) FROM dbo.MoneyOrders
UNION ALL SELECT 'Payments', COUNT(*) FROM dbo.Payments
UNION ALL SELECT 'IdentityVerifications', COUNT(*) FROM dbo.IdentityVerifications
UNION ALL SELECT 'BranchStock', COUNT(*) FROM dbo.BranchStock;

SELECT BranchCode, COUNT(*) AS Transactions,
       SUM(CASE WHEN Status = 'Voided' THEN 1 ELSE 0 END) AS Voided,
       SUM(CASE WHEN Status = 'Refunded' THEN 1 ELSE 0 END) AS Refunded,
       COUNT(DISTINCT CAST(OpenedAt AS date)) AS ActiveDays
FROM dbo.CounterTransactions
GROUP BY BranchCode
ORDER BY BranchCode;

SELECT tx.BranchCode, COUNT(*) AS MoneyOrders,
       SUM(CASE WHEN tx.Status = 'Paid' THEN 0 ELSE 1 END) AS Reversed,
       COUNT(DISTINCT CAST(tx.OpenedAt AS date)) AS ActiveDays
FROM dbo.MoneyOrders AS item
JOIN dbo.CounterTransactions AS tx ON tx.Id = item.TransactionId
GROUP BY tx.BranchCode
ORDER BY tx.BranchCode;

SELECT MIN(Branches) AS FewestBranchesOnADay, MAX(Branches) AS MostBranchesOnADay,
       MIN(Transactions) AS QuietestDay, MAX(Transactions) AS BusiestDay
FROM
(
    SELECT CAST(OpenedAt AS date) AS Day, COUNT(DISTINCT BranchCode) AS Branches, COUNT(*) AS Transactions
    FROM dbo.CounterTransactions
    WHERE CreatedBy = 'volume-seed'
    GROUP BY CAST(OpenedAt AS date)
) AS days;

SELECT 'Transactions without payment' AS CheckName, COUNT(*) AS Violations
FROM dbo.CounterTransactions AS tx
WHERE tx.CreatedBy = 'volume-seed'
  AND NOT EXISTS (SELECT 1 FROM dbo.Payments AS payment WHERE payment.TransactionId = tx.Id)
UNION ALL
SELECT 'Transactions without retail lines', COUNT(*)
FROM dbo.CounterTransactions AS tx
WHERE tx.CreatedBy = 'volume-seed'
  AND NOT EXISTS (SELECT 1 FROM dbo.TransactionLines AS line WHERE line.TransactionId = tx.Id)
UNION ALL
SELECT 'Money orders over 300 without passed check', COUNT(*)
FROM dbo.MoneyOrders AS item
WHERE item.Amount > 300
  AND item.CreatedBy = 'volume-seed'
  AND NOT EXISTS
  (
      SELECT 1 FROM dbo.IdentityVerifications AS checked
      WHERE checked.TransactionId = item.TransactionId AND checked.Result = 'Passed'
  )
UNION ALL
SELECT 'Fee differs from max(2, 2 percent)', COUNT(*)
FROM dbo.MoneyOrders
WHERE CreatedBy = 'volume-seed'
  AND Fee <> CASE WHEN ROUND(Amount * 0.02, 2) < 2 THEN 2 ELSE ROUND(Amount * 0.02, 2) END
UNION ALL
SELECT 'Letters over 2 kg', COUNT(*)
FROM dbo.Shipments
WHERE Type = 'Letter' AND WeightGrams > 2000
UNION ALL
SELECT 'Parcels over the level limit', COUNT(*)
FROM dbo.Shipments
WHERE Type = 'Parcel'
  AND WeightGrams > CASE Level WHEN 'Express' THEN 20000 WHEN 'Registered' THEN 10000 ELSE 30000 END
UNION ALL
SELECT 'Clerk not at home branch', COUNT(*)
FROM dbo.CounterTransactions AS tx
JOIN dbo.Staff AS clerk ON clerk.Id = tx.ClerkId
WHERE tx.CreatedBy = 'volume-seed' AND clerk.HomeBranch <> tx.BranchCode
UNION ALL
SELECT 'Counter of another branch', COUNT(*)
FROM dbo.CounterTransactions AS tx
JOIN dbo.Counters AS counter ON counter.Id = tx.CounterId
WHERE tx.CreatedBy = 'volume-seed' AND counter.BranchCode <> tx.BranchCode
UNION ALL
SELECT 'Volume rows dated today or later', COUNT(*)
FROM dbo.CounterTransactions
WHERE CreatedBy = 'volume-seed' AND OpenedAt >= CAST(CAST(CreatedAtUtc AS date) AS datetime2);
