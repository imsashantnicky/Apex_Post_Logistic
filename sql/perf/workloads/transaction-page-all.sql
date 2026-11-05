-- GET /Transactions as an Auditor: the page of section 1 of docs/perf/ef-tostring.sql without the branch filter.
SELECT [c].[Id], [c].[BranchCode] AS [Branch], [c].[OpenedAt], [c].[Status], [p].[TransactionId], [p].[CreatedAtUtc], [p].[CreatedBy], [p].[Method], [p].[ModifiedAtUtc], [p].[PaidAt], [p].[Tax]
FROM [CounterTransactions] AS [c]
LEFT JOIN [Payments] AS [p] ON [c].[Id] = [p].[TransactionId]
ORDER BY [c].[OpenedAt] DESC, [c].[Id] DESC
OFFSET @p ROWS FETCH NEXT @p2 ROWS ONLY
