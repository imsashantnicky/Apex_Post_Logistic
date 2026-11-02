/*
    Session B: reads the row, then changes it. Run this while session A waits. It reports its isolation level and how
    long each statement took, which is how long it was blocked.
*/
SET NOCOUNT ON;
DECLARE @Start datetime2 = SYSUTCDATETIME();
SELECT session_id AS SessionB,
       CASE transaction_isolation_level
           WHEN 1 THEN 'READ UNCOMMITTED' WHEN 2 THEN 'READ COMMITTED' WHEN 3 THEN 'REPEATABLE READ'
           WHEN 4 THEN 'SERIALIZABLE' WHEN 5 THEN 'SNAPSHOT'
       END AS IsolationLevel,
       (SELECT is_read_committed_snapshot_on FROM sys.databases WHERE database_id = DB_ID()) AS ReadCommittedSnapshot
FROM sys.dm_exec_sessions
WHERE session_id = @@SPID;

SELECT Quantity AS QuantityReadByB FROM dbo.BranchStock WHERE BranchCode = 'BR001' AND Sku = 'PKG-0002';
PRINT CONCAT('Read finished after ', DATEDIFF(MILLISECOND, @Start, SYSUTCDATETIME()), ' ms');

SET @Start = SYSUTCDATETIME();
BEGIN TRAN;
UPDATE dbo.BranchStock SET ModifiedAtUtc = ModifiedAtUtc WHERE BranchCode = 'BR001' AND Sku = 'PKG-0002';
ROLLBACK TRAN;
PRINT CONCAT('Update finished after ', DATEDIFF(MILLISECOND, @Start, SYSUTCDATETIME()), ' ms');
