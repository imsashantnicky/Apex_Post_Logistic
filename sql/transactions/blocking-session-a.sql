/*
    Session A: a sale in progress. It holds an exclusive lock on one stock row and does not commit yet. Open this and
    blocking-session-b.sql in two SSMS windows, or two sqlcmd sessions, and start A first. The WAITFOR stands for work
    done before the commit; ROLLBACK leaves the data as it was.
*/
SET NOCOUNT ON;
PRINT CONCAT('Session A is ', @@SPID, ' at ', CONVERT(varchar(12), SYSUTCDATETIME(), 114));
BEGIN TRAN;
UPDATE dbo.BranchStock SET Quantity = Quantity - 1 WHERE BranchCode = 'BR001' AND Sku = 'PKG-0002';
PRINT 'Session A holds the row lock; the change is not committed';
WAITFOR DELAY '00:00:08';
ROLLBACK TRAN;
PRINT CONCAT('Session A rolled back at ', CONVERT(varchar(12), SYSUTCDATETIME(), 114));
