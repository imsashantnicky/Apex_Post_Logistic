/*
    Money orders and identity verifications per branch. Every branch appears, with zeros if nothing happened. Only
    money orders still Paid count as issued; every identity check counts, whatever followed. Both are aggregated
    before the join: joined directly, a transaction with two checks would count its money order twice.
    Parameters: @From, @To (inclusive days), @MinActivity (hide quiet branches).
*/
DECLARE @From date = DATEADD(DAY, -14, CAST(SYSUTCDATETIME() AS date));
DECLARE @To date = CAST(SYSUTCDATETIME() AS date);
DECLARE @MinActivity int = 0;
-- query
WITH Issued AS
(
    SELECT tx.BranchCode, COUNT(*) AS MoneyOrders, SUM(item.Amount) AS Amount, SUM(item.Fee) AS Fees
    FROM dbo.Payments AS payment
    INNER JOIN dbo.CounterTransactions AS tx ON tx.Id = payment.TransactionId
    INNER JOIN dbo.MoneyOrders AS item ON item.TransactionId = tx.Id
    WHERE payment.PaidAt >= CAST(@From AS datetime2)
      AND payment.PaidAt < DATEADD(DAY, 1, CAST(@To AS datetime2))
      AND tx.Status = 'Paid'
    GROUP BY tx.BranchCode
),
Checks AS
(
    SELECT tx.BranchCode,
           SUM(CASE WHEN checked.Result = 'Passed' THEN 1 ELSE 0 END) AS Passed,
           SUM(CASE WHEN checked.Result = 'Failed' THEN 1 ELSE 0 END) AS Failed,
           SUM(CASE WHEN checked.Result = 'Pending' THEN 1 ELSE 0 END) AS Pending
    FROM dbo.IdentityVerifications AS checked
    INNER JOIN dbo.CounterTransactions AS tx ON tx.Id = checked.TransactionId
    WHERE checked.VerifiedAt >= CAST(@From AS datetime2)
      AND checked.VerifiedAt < DATEADD(DAY, 1, CAST(@To AS datetime2))
    GROUP BY tx.BranchCode
)
SELECT branch.Code AS BranchCode,
       branch.Name AS BranchName,
       COALESCE(MAX(issued.MoneyOrders), 0) AS MoneyOrders,
       COALESCE(MAX(issued.Amount), 0) AS Amount,
       COALESCE(MAX(issued.Fees), 0) AS Fees,
       COALESCE(MAX(checks.Passed), 0) AS Passed,
       COALESCE(MAX(checks.Failed), 0) AS Failed,
       COALESCE(MAX(checks.Pending), 0) AS Pending
FROM dbo.Branches AS branch
LEFT JOIN Issued AS issued ON issued.BranchCode = branch.Code
LEFT JOIN Checks AS checks ON checks.BranchCode = branch.Code
GROUP BY branch.Code, branch.Name
HAVING COALESCE(MAX(issued.MoneyOrders), 0) + COALESCE(MAX(checks.Passed), 0) + COALESCE(MAX(checks.Failed), 0)
       + COALESCE(MAX(checks.Pending), 0) >= @MinActivity
ORDER BY branch.Code;
