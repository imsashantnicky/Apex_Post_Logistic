-- Original period filter, all branches (Auditor): money-orders-case.sql without the branch filter.
SELECT COUNT(*)
FROM [CounterTransactions] AS [c]
LEFT JOIN [Payments] AS [p] ON [c].[Id] = [p].[TransactionId]
WHERE CASE
    WHEN [p].[TransactionId] IS NOT NULL THEN [p].[PaidAt]
    ELSE [c].[OpenedAt]
END >= @start AND CASE
    WHEN [p].[TransactionId] IS NOT NULL THEN [p].[PaidAt]
    ELSE [c].[OpenedAt]
END < @end AND (EXISTS (
    SELECT 1
    FROM [MoneyOrders] AS [m]
    WHERE [m].[TransactionId] = [c].[Id]) OR EXISTS (
    SELECT 1
    FROM [IdentityVerifications] AS [i]
    WHERE [i].[TransactionId] = [c].[Id]))
