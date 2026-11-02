/*
    A manual stock correction and its StockAdjustments history row, committed together.

    A single UPDATE computes the new quantity under the row's lock, so two managers cannot overwrite each other and
    no read-then-write window exists. The first delivery to a branch creates the row. Role and branch ownership stay
    in StockService, which runs before this; this procedure protects the data. See ADR-012 in DESIGN.md.

    Errors: 51021 invalid input, 51022 unknown branch, 51023 unknown product, 51024 not enough stock.
*/
CREATE OR ALTER PROCEDURE dbo.usp_AdjustStock
    @BranchCode varchar(5),
    @Sku varchar(8),
    @Change int,
    @Reason nvarchar(500),
    @StaffId uniqueidentifier,
    @AdjustedAt datetime2,
    @NewQuantity int = NULL OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @BranchCode IS NULL OR @Sku IS NULL OR @StaffId IS NULL OR @AdjustedAt IS NULL
        THROW 51021, 'usp_AdjustStock: branch, SKU, staff and time are required.', 1;

    IF @Change IS NULL OR @Change = 0
        THROW 51021, 'usp_AdjustStock: @Change must not be zero.', 1;

    IF NULLIF(LTRIM(RTRIM(@Reason)), N'') IS NULL
        THROW 51021, 'usp_AdjustStock: a reason is required.', 1;

    IF NOT EXISTS (SELECT 1 FROM dbo.Branches WHERE Code = @BranchCode)
        THROW 51022, 'usp_AdjustStock: the branch does not exist.', 1;

    IF NOT EXISTS (SELECT 1 FROM dbo.Products WHERE Sku = @Sku)
        THROW 51023, 'usp_AdjustStock: the product does not exist.', 1;

    DECLARE @CreatedBy varchar(50) = CAST(@StaffId AS varchar(36));

    BEGIN TRY
        BEGIN TRANSACTION;

        UPDATE dbo.BranchStock
        SET @NewQuantity = Quantity = Quantity + @Change,
            ModifiedAtUtc = @AdjustedAt
        WHERE BranchCode = @BranchCode
          AND Sku = @Sku
          AND Quantity + @Change >= 0;

        IF @@ROWCOUNT = 0
        BEGIN
            -- The row is missing or too low. Only a first delivery may go on, and only by creating the row.
            IF @Change < 0 OR EXISTS (SELECT 1 FROM dbo.BranchStock WHERE BranchCode = @BranchCode AND Sku = @Sku)
                THROW 51024, 'usp_AdjustStock: not enough stock.', 1;

            INSERT dbo.BranchStock (BranchCode, Sku, Quantity, CreatedAtUtc, CreatedBy)
            VALUES (@BranchCode, @Sku, @Change, @AdjustedAt, @CreatedBy);

            SET @NewQuantity = @Change;
        END;

        INSERT dbo.StockAdjustments
            (Id, BranchCode, Sku, Change, NewQuantity, Reason, StaffId, AdjustedAt, CreatedAtUtc, CreatedBy)
        VALUES
            (NEWID(), @BranchCode, @Sku, @Change, @NewQuantity, LTRIM(RTRIM(@Reason)), @StaffId, @AdjustedAt,
             @AdjustedAt, @CreatedBy);

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0
            ROLLBACK TRANSACTION;

        THROW;
    END CATCH;
END;
