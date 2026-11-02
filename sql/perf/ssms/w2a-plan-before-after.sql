/*
    W2a, page 1 of the transaction list of a branch, before and after its index, for the graphical actual plan in SSMS.

    In SSMS: connect to the server, choose ApexPostDb_Perf, press Ctrl+M (Include Actual Execution Plan), then F5.
    The Execution plan tab shows "Query 1" (before) and "Query 2" (after); the Messages tab shows the reads.

    Before is the baseline of PERFORMANCE.md: the indexes of migration AddWorkloadIndexes on CounterTransactions are
    removed and the foreign key index on BranchCode is back. That happens inside a transaction that is rolled back, so
    the database ends as it started. The same statement and parameters as Measure-Workloads.ps1 (W2a).
*/
SET NOCOUNT ON;
SET STATISTICS IO ON;

DECLARE @Query nvarchar(max) = N'
SELECT [c].[Id], [c].[BranchCode] AS [Branch], [c].[OpenedAt], [c].[Status], [p].[TransactionId], [p].[CreatedAtUtc],
       [p].[CreatedBy], [p].[Method], [p].[ModifiedAtUtc], [p].[PaidAt], [p].[Tax]
FROM [CounterTransactions] AS [c]
LEFT JOIN [Payments] AS [p] ON [c].[Id] = [p].[TransactionId]
WHERE [c].[BranchCode] = @branch
ORDER BY [c].[OpenedAt] DESC, [c].[Id] DESC
OFFSET @p ROWS FETCH NEXT @p2 ROWS ONLY';
DECLARE @Parameters nvarchar(200) = N'@branch varchar(5), @p int, @p2 int';

BEGIN TRANSACTION;

DROP INDEX IX_CounterTransactions_BranchCode_OpenedAt ON dbo.CounterTransactions;
DROP INDEX IX_CounterTransactions_OpenedAt ON dbo.CounterTransactions;
CREATE INDEX IX_CounterTransactions_BranchCode ON dbo.CounterTransactions (BranchCode);

PRINT '-- Query 1: before, no workload index';
EXEC sys.sp_executesql @Query, @Parameters, @branch = 'BR001', @p = 0, @p2 = 20;

ROLLBACK TRANSACTION;

PRINT '-- Query 2: after, IX_CounterTransactions_BranchCode_OpenedAt';
EXEC sys.sp_executesql @Query, @Parameters, @branch = 'BR001', @p = 0, @p2 = 20;

SET STATISTICS IO OFF;
