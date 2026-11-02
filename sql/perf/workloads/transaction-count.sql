-- The total above the transaction list: CountAsync over the same filter.
SELECT COUNT(*)
FROM [CounterTransactions] AS [c]
WHERE [c].[BranchCode] = @branch
