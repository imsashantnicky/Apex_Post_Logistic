-- GET /Transactions as a Branch Manager. Generated SQL, docs/perf/ef-tostring.sql section 1.
SELECT [c].[Id], [c].[BranchCode] AS [Branch], [c].[OpenedAt], [c].[Status], [p].[TransactionId], [p].[CreatedAtUtc], [p].[CreatedBy], [p].[Method], [p].[ModifiedAtUtc], [p].[PaidAt], [p].[Tax]
FROM [CounterTransactions] AS [c]
LEFT JOIN [Payments] AS [p] ON [c].[Id] = [p].[TransactionId]
WHERE [c].[BranchCode] = @branch
ORDER BY [c].[OpenedAt] DESC, [c].[Id] DESC
OFFSET @p ROWS FETCH NEXT @p2 ROWS ONLY
