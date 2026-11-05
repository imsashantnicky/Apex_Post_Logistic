/*
    Active branches with no paid sale on a day. Closed branches are left out: they cannot sell.
    The filter stays in the ON clause, so that a branch without sales keeps its row for HAVING to find.
    Parameter: @Day, today by default.
*/
DECLARE @Day date = CAST(SYSUTCDATETIME() AS date);
-- query
SELECT branch.Code AS BranchCode, branch.Name AS BranchName, branch.Region, COUNT(payment.TransactionId) AS Sales
FROM dbo.Branches AS branch
LEFT JOIN dbo.CounterTransactions AS tx
    ON tx.BranchCode = branch.Code
   AND tx.Status = 'Paid'
LEFT JOIN dbo.Payments AS payment
    ON payment.TransactionId = tx.Id
   AND payment.PaidAt >= CAST(@Day AS datetime2)
   AND payment.PaidAt < DATEADD(DAY, 1, CAST(@Day AS datetime2))
WHERE branch.IsActive = 1
GROUP BY branch.Code, branch.Name, branch.Region
HAVING COUNT(payment.TransactionId) = 0
ORDER BY branch.Code;
