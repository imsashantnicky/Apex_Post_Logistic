-- The fixed period filter: COUNT over the query of docs/perf/ef-tostring.sql section 4.
SELECT COUNT(*)
FROM (
    SELECT [c].[Id]
    FROM [CounterTransactions] AS [c]
    LEFT JOIN [Payments] AS [p] ON [c].[Id] = [p].[TransactionId]
    WHERE [c].[BranchCode] = @branch AND [p].[TransactionId] IS NOT NULL AND [p].[PaidAt] >= @start AND [p].[PaidAt] < @end
    UNION ALL
    SELECT [c0].[Id]
    FROM [CounterTransactions] AS [c0]
    LEFT JOIN [Payments] AS [p0] ON [c0].[Id] = [p0].[TransactionId]
    WHERE [c0].[BranchCode] = @branch AND [p0].[TransactionId] IS NULL AND [c0].[OpenedAt] >= @start0 AND [c0].[OpenedAt] < @end0
) AS [u]
WHERE EXISTS (
    SELECT 1
    FROM [MoneyOrders] AS [m]
    WHERE [m].[TransactionId] = [u].[Id]) OR EXISTS (
    SELECT 1
    FROM [IdentityVerifications] AS [i]
    WHERE [i].[TransactionId] = [u].[Id])
