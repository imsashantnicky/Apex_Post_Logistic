/*
    The procedures with valid and invalid calls, in the signatures of the brief. The adjustment is rolled back.
        sqlcmd -S localhost -E -C -I -d ApexPostDb_Perf -W -s "|" -i sql/perf/procedure-checks.sql
*/
SET NOCOUNT ON;

PRINT '-- usp_DailyBranchSales (brief signature), one branch, one day';
EXEC dbo.usp_DailyBranchSales @BranchCode = 'BR001', @Date = '2026-10-31';

PRINT '-- usp_DailyBranchSalesRange, the same branch over three days (what the reports call)';
EXEC dbo.usp_DailyBranchSalesRange @From = '2026-10-30', @To = '2026-11-01', @BranchCode = 'BR001';

PRINT '-- usp_LowStockReport, one branch';
EXEC dbo.usp_LowStockReport @Threshold = 5, @BranchCode = 'BR002';

PRINT '-- usp_AdjustStock, rolled back by the caller';
DECLARE @Staff uniqueidentifier =
    (SELECT TOP (1) Id FROM dbo.Staff WHERE Role = 'BranchManager' AND HomeBranch = 'BR001');
DECLARE @New int, @Before int = (SELECT COUNT(*) FROM dbo.StockAdjustments);
BEGIN TRAN;
EXEC dbo.usp_AdjustStock @BranchCode = 'BR001', @Sku = 'OFF-0001', @Delta = 2, @Reason = N'Evidence run',
    @ChangedBy = @Staff, @NewQuantity = @New OUTPUT;
SELECT @New AS NewQuantity, (SELECT COUNT(*) FROM dbo.StockAdjustments) - @Before AS NewHistoryRows;
ROLLBACK;

PRINT '-- parameter validation and THROW';
BEGIN TRY
    EXEC dbo.usp_DailyBranchSales @BranchCode = 'BR001', @Date = NULL;
END TRY
BEGIN CATCH
    PRINT CONCAT(ERROR_NUMBER(), ': ', ERROR_MESSAGE());
END CATCH;
BEGIN TRY
    EXEC dbo.usp_DailyBranchSalesRange @From = NULL, @To = '2026-11-01';
END TRY
BEGIN CATCH
    PRINT CONCAT(ERROR_NUMBER(), ': ', ERROR_MESSAGE());
END CATCH;
BEGIN TRY
    EXEC dbo.usp_DailyBranchSalesRange @From = '2026-11-02', @To = '2026-11-01';
END TRY
BEGIN CATCH
    PRINT CONCAT(ERROR_NUMBER(), ': ', ERROR_MESSAGE());
END CATCH;
BEGIN TRY
    EXEC dbo.usp_DailyBranchSalesRange @From = '2026-11-01', @To = '2026-11-01', @BranchCode = 'BR999';
END TRY
BEGIN CATCH
    PRINT CONCAT(ERROR_NUMBER(), ': ', ERROR_MESSAGE());
END CATCH;
BEGIN TRY
    EXEC dbo.usp_LowStockReport @Threshold = -1;
END TRY
BEGIN CATCH
    PRINT CONCAT(ERROR_NUMBER(), ': ', ERROR_MESSAGE());
END CATCH;
BEGIN TRY
    EXEC dbo.usp_LowStockReport @Threshold = 5, @BranchCode = 'BR999';
END TRY
BEGIN CATCH
    PRINT CONCAT(ERROR_NUMBER(), ': ', ERROR_MESSAGE());
END CATCH;
BEGIN TRY
    EXEC dbo.usp_AdjustStock @BranchCode = 'BR001', @Sku = 'OFF-0001', @Delta = 0, @Reason = N'x',
        @ChangedBy = @Staff;
END TRY
BEGIN CATCH
    PRINT CONCAT(ERROR_NUMBER(), ': ', ERROR_MESSAGE());
END CATCH;
BEGIN TRY
    EXEC dbo.usp_AdjustStock @BranchCode = 'BR001', @Sku = 'OFF-0001', @Delta = 1, @Reason = N' ',
        @ChangedBy = @Staff;
END TRY
BEGIN CATCH
    PRINT CONCAT(ERROR_NUMBER(), ': ', ERROR_MESSAGE());
END CATCH;
BEGIN TRY
    EXEC dbo.usp_AdjustStock @BranchCode = 'BR999', @Sku = 'OFF-0001', @Delta = 1, @Reason = N'x',
        @ChangedBy = @Staff;
END TRY
BEGIN CATCH
    PRINT CONCAT(ERROR_NUMBER(), ': ', ERROR_MESSAGE());
END CATCH;
BEGIN TRY
    EXEC dbo.usp_AdjustStock @BranchCode = 'BR001', @Sku = 'PKG-9999', @Delta = 1, @Reason = N'x',
        @ChangedBy = @Staff;
END TRY
BEGIN CATCH
    PRINT CONCAT(ERROR_NUMBER(), ': ', ERROR_MESSAGE());
END CATCH;
BEGIN TRY
    EXEC dbo.usp_AdjustStock @BranchCode = 'BR001', @Sku = 'OFF-0001', @Delta = -100000, @Reason = N'x',
        @ChangedBy = @Staff;
END TRY
BEGIN CATCH
    PRINT CONCAT(ERROR_NUMBER(), ': ', ERROR_MESSAGE());
END CATCH;
