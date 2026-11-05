-- GET /Reports/ShippingVolume. Generated SQL, docs/perf/ef-tostring.sql section 2.
SELECT [s].[ZoneCode], [s].[Level], [s].[WeightGrams] AS [Weight], [s].[LengthCm], [s].[WidthCm], [s].[HeightCm], COUNT(*) AS [Count], COALESCE(SUM(CAST([s].[Price] AS decimal(18,2))), 0.0) AS [Revenue]
FROM [Shipments] AS [s]
INNER JOIN (
    SELECT [c].[Id]
    FROM [CounterTransactions] AS [c]
    LEFT JOIN [Payments] AS [p] ON [c].[Id] = [p].[TransactionId]
    WHERE [c].[Status] = 'Paid' AND [p].[PaidAt] >= @start AND [p].[PaidAt] < @end AND [c].[BranchCode] = @branch
) AS [s0] ON [s].[TransactionId] = [s0].[Id]
GROUP BY [s].[ZoneCode], [s].[Level], [s].[WeightGrams], [s].[LengthCm], [s].[WidthCm], [s].[HeightCm]
