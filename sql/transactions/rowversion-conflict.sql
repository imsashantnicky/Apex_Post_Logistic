-- Two sessions want the last unit of one stock row. This is the statement EF Core sends for each: an UPDATE that only
-- matches the RowVersion its session read. The second session is simulated in order, so the output is repeatable.
-- Run it on a throw-away database: it changes one stock row and restores the quantity at the end.
SET NOCOUNT ON;

DECLARE @Branch varchar(5) = 'BR001';
DECLARE @Sku varchar(8) = 'PKG-0003';
DECLARE @Original int = (SELECT Quantity FROM dbo.BranchStock WHERE BranchCode = @Branch AND Sku = @Sku);

UPDATE dbo.BranchStock SET Quantity = 1 WHERE BranchCode = @Branch AND Sku = @Sku;

DECLARE @SessionA binary(8) = (SELECT RowVersion FROM dbo.BranchStock WHERE BranchCode = @Branch AND Sku = @Sku);
DECLARE @SessionB binary(8) = (SELECT RowVersion FROM dbo.BranchStock WHERE BranchCode = @Branch AND Sku = @Sku);
SELECT 'Both sessions read' AS Step, Quantity, sys.fn_varbintohexstr(CAST(RowVersion AS varbinary(8))) AS RowVersion
FROM dbo.BranchStock WHERE BranchCode = @Branch AND Sku = @Sku;

UPDATE dbo.BranchStock SET Quantity = 0
WHERE BranchCode = @Branch AND Sku = @Sku AND RowVersion = @SessionA;
SELECT 'Session A sold the last unit' AS Step, @@ROWCOUNT AS RowsChanged;

UPDATE dbo.BranchStock SET Quantity = 0
WHERE BranchCode = @Branch AND Sku = @Sku AND RowVersion = @SessionB;
SELECT 'Session B used the old RowVersion' AS Step, @@ROWCOUNT AS RowsChanged;

SELECT 'Zero rows is what EF Core reports as DbUpdateConcurrencyException, answered with 409' AS Meaning;

SELECT 'After both' AS Step, Quantity, sys.fn_varbintohexstr(CAST(RowVersion AS varbinary(8))) AS RowVersion
FROM dbo.BranchStock WHERE BranchCode = @Branch AND Sku = @Sku;

BEGIN TRY
    UPDATE dbo.BranchStock SET Quantity = -1 WHERE BranchCode = @Branch AND Sku = @Sku;
END TRY
BEGIN CATCH
    SELECT 'Going below zero, even without a version check' AS Step, ERROR_NUMBER() AS ErrorNumber,
           ERROR_MESSAGE() AS ErrorText;
END CATCH;

UPDATE dbo.BranchStock SET Quantity = @Original WHERE BranchCode = @Branch AND Sku = @Sku;
