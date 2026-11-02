/*
    A manual stock correction and its StockAdjustments history row, committed together.

    A single UPDATE computes the new quantity under the row's lock, so two managers cannot overwrite each other and
    no read-then-write window exists. The first delivery to a branch creates the row. Role and branch ownership stay
    in StockService, which runs before this; this procedure protects the data. See ADR-012 in DESIGN.md.

    @Delta is the signed change, @ChangedBy the staff member who made it. @AdjustedAt defaults to now; the application
    passes its own clock so tests can fix the time.

    Errors: 51021 invalid input, 51022 unknown branch, 51023 unknown product, 51024 not enough stock.
*/
CREATE OR ALTER PROCEDURE dbo.usp_AdjustStock
    @BranchCode varchar(5),
    @Sku varchar(8),
    @Delta int,
    @Reason nvarchar(500),
    @ChangedBy uniqueidentifier,
    @AdjustedAt datetime2 = NULL,
    @NewQuantity int = NULL OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @BranchCode IS NULL OR @Sku IS NULL OR @ChangedBy IS NULL
        THROW 51021, 'usp_AdjustStock: branch, SKU and @ChangedBy are required.', 1;

    IF @Delta IS NULL OR @Delta = 0
        THROW 51021, 'usp_AdjustStock: @Delta must not be zero.', 1;

    IF NULLIF(LTRIM(RTRIM(@Reason)), N'') IS NULL
        THROW 51021, 'usp_AdjustStock: a reason is required.', 1;

    IF NOT EXISTS (SELECT 1 FROM dbo.Branches WHERE Code = @BranchCode)
        THROW 51022, 'usp_AdjustStock: the branch does not exist.', 1;

    IF NOT EXISTS (SELECT 1 FROM dbo.Products WHERE Sku = @Sku)
        THROW 51023, 'usp_AdjustStock: the product does not exist.', 1;

    DECLARE @At datetime2 = COALESCE(@AdjustedAt, SYSUTCDATETIME());
    DECLARE @CreatedBy varchar(50) = CAST(@ChangedBy AS varchar(36));

    BEGIN TRY
        BEGIN TRANSACTION;

        UPDATE dbo.BranchStock
        SET @NewQuantity = Quantity = Quantity + @Delta,
            ModifiedAtUtc = @At
        WHERE BranchCode = @BranchCode
          AND Sku = @Sku
          AND Quantity + @Delta >= 0;

        IF @@ROWCOUNT = 0
        BEGIN
            -- The row is missing or too low. Only a first delivery may go on, and only by creating the row.
            IF @Delta < 0 OR EXISTS (SELECT 1 FROM dbo.BranchStock WHERE BranchCode = @BranchCode AND Sku = @Sku)
                THROW 51024, 'usp_AdjustStock: not enough stock.', 1;

            INSERT dbo.BranchStock (BranchCode, Sku, Quantity, CreatedAtUtc, CreatedBy)
            VALUES (@BranchCode, @Sku, @Delta, @At, @CreatedBy);

            SET @NewQuantity = @Delta;
        END;

        INSERT dbo.StockAdjustments
            (Id, BranchCode, Sku, Change, NewQuantity, Reason, StaffId, AdjustedAt, CreatedAtUtc, CreatedBy)
        VALUES
            (NEWID(), @BranchCode, @Sku, @Delta, @NewQuantity, LTRIM(RTRIM(@Reason)), @ChangedBy, @At, @At,
             @CreatedBy);

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0
            ROLLBACK TRANSACTION;

        THROW;
    END CATCH;
END;
