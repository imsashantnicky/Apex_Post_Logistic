-- Fixed period filter, all branches (Auditor): money-orders-sarg.sql without the branch filter.
SELECT COUNT(*)
FROM (
    SELECT [c].[Id]
    FROM [CounterTransactions] AS [c]
    LEFT JOIN [Payments] AS [p] ON [c].[Id] = [p].[TransactionId]
    WHERE [p].[TransactionId] IS NOT NULL AND [p].[PaidAt] >= @start AND [p].[PaidAt] < @end
    UNION ALL
    SELECT [c0].[Id]
    FROM [CounterTransactions] AS [c0]
    LEFT JOIN [Payments] AS [p0] ON [c0].[Id] = [p0].[TransactionId]
    WHERE [p0].[TransactionId] IS NULL AND [c0].[OpenedAt] >= @start0 AND [c0].[OpenedAt] < @end0
) AS [u]
WHERE EXISTS (
    SELECT 1
    FROM [MoneyOrders] AS [m]
    WHERE [m].[TransactionId] = [u].[Id]) OR EXISTS (
    SELECT 1
    FROM [IdentityVerifications] AS [i]
    WHERE [i].[TransactionId] = [u].[Id])
