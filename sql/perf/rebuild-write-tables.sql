/*
    Compacts the tables the write workload touches, so that every index set starts from the same physical state.
    A rollback keeps the page splits of the inserts, so without this each run would start where the last one ended.
*/
SET NOCOUNT ON;
ALTER INDEX ALL ON dbo.CounterTransactions REBUILD;
ALTER INDEX ALL ON dbo.Payments REBUILD;
ALTER INDEX ALL ON dbo.TransactionLines REBUILD;
ALTER INDEX ALL ON dbo.Shipments REBUILD;
