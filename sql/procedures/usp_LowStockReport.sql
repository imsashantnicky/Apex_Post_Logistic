/*
    Stock at or below the threshold, lowest first. Backs /Reports/LowStock and the dashboard.

    @BranchCode NULL means all branches. The two shapes are two statements: the choice between them and between
    OPTION (RECOMPILE) is measured in PERFORMANCE.md and decided in ADR-012.
*/
CREATE OR ALTER PROCEDURE dbo.usp_LowStockReport
    @Threshold int,
    @BranchCode varchar(5) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF @Threshold IS NULL OR @Threshold < 0
        THROW 51011, 'usp_LowStockReport: @Threshold must be zero or more.', 1;

    IF @BranchCode IS NOT NULL AND NOT EXISTS (SELECT 1 FROM dbo.Branches WHERE Code = @BranchCode)
        THROW 51012, 'usp_LowStockReport: the branch does not exist.', 1;

    IF @BranchCode IS NULL
    BEGIN
        SELECT stock.BranchCode, stock.Sku, product.Name AS Product, stock.Quantity
        FROM dbo.BranchStock AS stock
        INNER JOIN dbo.Products AS product ON product.Sku = stock.Sku
        WHERE stock.Quantity <= @Threshold
        ORDER BY stock.Quantity, stock.BranchCode, stock.Sku;
    END
    ELSE
    BEGIN
        SELECT stock.BranchCode, stock.Sku, product.Name AS Product, stock.Quantity
        FROM dbo.BranchStock AS stock
        INNER JOIN dbo.Products AS product ON product.Sku = stock.Sku
        WHERE stock.BranchCode = @BranchCode
          AND stock.Quantity <= @Threshold
        ORDER BY stock.Quantity, stock.BranchCode, stock.Sku;
    END;
END;
