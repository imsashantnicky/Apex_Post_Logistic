-- The original period filter of the money order report: COUNT over the WHERE clause of
-- docs/perf/ef-tostring-before-sarg-fix.sql section 4. CASE over two columns cannot use an index on either.
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
    WHERE [i].[TransactionId] = [c].[Id])) AND [c].[BranchCode] = @branch
