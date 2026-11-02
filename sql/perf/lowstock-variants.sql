/*
    Decides how usp_LowStockReport handles its optional branch: three shapes, 5000 calls each, half for one branch and
    half for all branches, the mix of the dashboard and the report. Runs on a performance database only and removes its
    own procedures. CPU is the session's own cpu_time from sys.dm_exec_requests, so it includes compilation, which the
    per-statement counters leave out.
        sqlcmd -S localhost -E -C -I -d ApexPostDb_Perf -W -s "|" -i sql/perf/lowstock-variants.sql
*/
SET NOCOUNT ON;
GO
CREATE OR ALTER PROCEDURE dbo.perf_LowStock_CatchAll @Threshold int, @BranchCode varchar(5) = NULL
AS
SELECT stock.BranchCode, stock.Sku, product.Name, stock.Quantity
FROM dbo.BranchStock AS stock
INNER JOIN dbo.Products AS product ON product.Sku = stock.Sku
WHERE (@BranchCode IS NULL OR stock.BranchCode = @BranchCode) AND stock.Quantity <= @Threshold
ORDER BY stock.Quantity, stock.BranchCode, stock.Sku;
GO
CREATE OR ALTER PROCEDURE dbo.perf_LowStock_Recompile @Threshold int, @BranchCode varchar(5) = NULL
AS
SELECT stock.BranchCode, stock.Sku, product.Name, stock.Quantity
FROM dbo.BranchStock AS stock
INNER JOIN dbo.Products AS product ON product.Sku = stock.Sku
WHERE (@BranchCode IS NULL OR stock.BranchCode = @BranchCode) AND stock.Quantity <= @Threshold
ORDER BY stock.Quantity, stock.BranchCode, stock.Sku
OPTION (RECOMPILE);
GO
CREATE TABLE #Sink (BranchCode varchar(5), Sku varchar(8), Product nvarchar(100), Quantity int);
CREATE TABLE #Result (Variant varchar(30), Calls int, CpuMs int, ElapsedMs int, MicrosecondsPerCall int);

DECLARE @Variants TABLE (Name sysname, Ordinal int);
-- Twice round, so that the first pass warms the plans; only the second counts.
INSERT @Variants VALUES ('perf_LowStock_CatchAll', 1), ('perf_LowStock_Recompile', 2), ('usp_LowStockReport', 3);

DECLARE @Pass int = 1;
WHILE @Pass <= 2
BEGIN
    DECLARE @Ordinal int = 1;
    WHILE @Ordinal <= 3
    BEGIN
        DECLARE @Procedure nvarchar(300) = N'dbo.' + (SELECT Name FROM @Variants WHERE Ordinal = @Ordinal);
        DECLARE @Calls int = 5000, @Call int = 0;
        DECLARE @Branch varchar(5);
        DECLARE @Cpu int = (SELECT cpu_time FROM sys.dm_exec_requests WHERE session_id = @@SPID);
        DECLARE @Start datetime2 = SYSUTCDATETIME();

        WHILE @Call < @Calls
        BEGIN
            SET @Branch = CASE WHEN @Call % 2 = 0 THEN NULL ELSE 'BR001' END;
            INSERT #Sink EXEC @Procedure @Threshold = 5, @BranchCode = @Branch;
            TRUNCATE TABLE #Sink;
            SET @Call += 1;
        END;

        IF @Pass = 2
            INSERT #Result
            SELECT REPLACE(@Procedure, N'dbo.', N''), @Calls,
                   (SELECT cpu_time FROM sys.dm_exec_requests WHERE session_id = @@SPID) - @Cpu,
                   DATEDIFF(MILLISECOND, @Start, SYSUTCDATETIME()),
                   DATEDIFF(MICROSECOND, @Start, SYSUTCDATETIME()) / @Calls;

        SET @Ordinal += 1;
    END;
    SET @Pass += 1;
END;

SELECT Variant, Calls, CpuMs, ElapsedMs, MicrosecondsPerCall FROM #Result ORDER BY Variant;

SET STATISTICS IO ON;
EXEC dbo.perf_LowStock_CatchAll @Threshold = 5, @BranchCode = 'BR001';
EXEC dbo.perf_LowStock_Recompile @Threshold = 5, @BranchCode = 'BR001';
EXEC dbo.usp_LowStockReport @Threshold = 5, @BranchCode = 'BR001';
SET STATISTICS IO OFF;

DROP TABLE #Sink;
DROP TABLE #Result;
GO
DROP PROCEDURE dbo.perf_LowStock_CatchAll;
DROP PROCEDURE dbo.perf_LowStock_Recompile;
GO
