/*
    What the schema really enforces, read from the catalog and tried out. Everything is rolled back.
        sqlcmd -S localhost -E -C -I -d ApexPostDb_Perf -W -s "|" -i sql/perf/schema-checks.sql
*/
SET NOCOUNT ON;

SELECT COUNT(*) AS ApplicationTables FROM sys.tables WHERE name <> '__EFMigrationsHistory';

SELECT delete_referential_action_desc AS OnDelete, COUNT(*) AS ForeignKeys
FROM sys.foreign_keys
GROUP BY delete_referential_action_desc;

SELECT OBJECT_NAME(parent_object_id) AS Child, OBJECT_NAME(referenced_object_id) AS Parent,
       name AS ForeignKey, delete_referential_action_desc AS OnDelete
FROM sys.foreign_keys
ORDER BY delete_referential_action_desc, Child, Parent;

SELECT COUNT(*) AS CheckConstraints FROM sys.check_constraints;
SELECT COUNT(*) AS UniqueIndexesOutsidePrimaryKeys FROM sys.indexes WHERE is_unique = 1 AND is_primary_key = 0
    AND OBJECT_NAME(object_id) NOT LIKE 'sys%' AND object_id IN (SELECT object_id FROM sys.tables);

-- The index catalogue of PERFORMANCE.md, from the database itself.
SELECT tableInfo.name AS TableName, indexInfo.name AS IndexName, indexInfo.is_unique AS IsUnique,
       (SELECT STRING_AGG(column_name, ', ') WITHIN GROUP (ORDER BY key_ordinal)
        FROM (SELECT columnInfo.name AS column_name, keyColumn.key_ordinal
              FROM sys.index_columns AS keyColumn
              JOIN sys.columns AS columnInfo
                  ON columnInfo.object_id = keyColumn.object_id AND columnInfo.column_id = keyColumn.column_id
              WHERE keyColumn.object_id = indexInfo.object_id AND keyColumn.index_id = indexInfo.index_id
                AND keyColumn.is_included_column = 0) AS keys) AS KeyColumns,
       COALESCE((SELECT STRING_AGG(columnInfo.name, ', ')
                 FROM sys.index_columns AS keyColumn
                 JOIN sys.columns AS columnInfo
                     ON columnInfo.object_id = keyColumn.object_id AND columnInfo.column_id = keyColumn.column_id
                 WHERE keyColumn.object_id = indexInfo.object_id AND keyColumn.index_id = indexInfo.index_id
                   AND keyColumn.is_included_column = 1), '-') AS IncludedColumns,
       COALESCE(indexInfo.filter_definition, '-') AS Filter
FROM sys.indexes AS indexInfo
JOIN sys.tables AS tableInfo ON tableInfo.object_id = indexInfo.object_id
WHERE indexInfo.is_primary_key = 0
  AND indexInfo.type > 0
  AND tableInfo.name <> '__EFMigrationsHistory'
ORDER BY tableInfo.name, indexInfo.name;

PRINT '-- Restrict: a branch with history cannot be deleted';
BEGIN TRY
    DELETE FROM dbo.Branches WHERE Code = 'BR001';
END TRY
BEGIN CATCH
    PRINT CONCAT(ERROR_NUMBER(), ': ', LEFT(ERROR_MESSAGE(), 110));
END CATCH;

PRINT '-- Restrict: a product that was sold cannot be deleted';
BEGIN TRY
    DELETE FROM dbo.Products WHERE Sku = 'PKG-0001';
END TRY
BEGIN CATCH
    PRINT CONCAT(ERROR_NUMBER(), ': ', LEFT(ERROR_MESSAGE(), 110));
END CATCH;

PRINT '-- Restrict: a clerk with transactions cannot be deleted';
BEGIN TRY
    DELETE FROM dbo.Staff WHERE Id = (SELECT TOP (1) ClerkId FROM dbo.CounterTransactions);
END TRY
BEGIN CATCH
    PRINT CONCAT(ERROR_NUMBER(), ': ', LEFT(ERROR_MESSAGE(), 110));
END CATCH;

PRINT '-- Cascade: deleting a transaction takes its own lines and payment, nothing else';
BEGIN TRAN;
DECLARE @Transaction uniqueidentifier =
(
    SELECT TOP (1) tx.Id
    FROM dbo.CounterTransactions AS tx
    WHERE EXISTS (SELECT 1 FROM dbo.Payments AS payment WHERE payment.TransactionId = tx.Id)
      AND EXISTS (SELECT 1 FROM dbo.Shipments AS shipment WHERE shipment.TransactionId = tx.Id)
      AND NOT EXISTS (SELECT 1 FROM dbo.IdentityVerifications AS checked WHERE checked.TransactionId = tx.Id)
);
SELECT (SELECT COUNT(*) FROM dbo.TransactionLines WHERE TransactionId = @Transaction) AS LinesBefore,
       (SELECT COUNT(*) FROM dbo.Shipments WHERE TransactionId = @Transaction) AS ShipmentsBefore,
       (SELECT COUNT(*) FROM dbo.Payments WHERE TransactionId = @Transaction) AS PaymentsBefore;
DELETE FROM dbo.CounterTransactions WHERE Id = @Transaction;
SELECT (SELECT COUNT(*) FROM dbo.TransactionLines WHERE TransactionId = @Transaction) AS LinesAfter,
       (SELECT COUNT(*) FROM dbo.Shipments WHERE TransactionId = @Transaction) AS ShipmentsAfter,
       (SELECT COUNT(*) FROM dbo.Payments WHERE TransactionId = @Transaction) AS PaymentsAfter;
ROLLBACK;

PRINT '-- Restrict: a transaction with an identity check keeps it, so it cannot be deleted';
BEGIN TRY
    DELETE FROM dbo.CounterTransactions
    WHERE Id = (SELECT TOP (1) TransactionId FROM dbo.IdentityVerifications);
END TRY
BEGIN CATCH
    PRINT CONCAT(ERROR_NUMBER(), ': ', LEFT(ERROR_MESSAGE(), 110));
END CATCH;

PRINT '-- CHECK constraints refuse what the domain would never send';
BEGIN TRY
    UPDATE dbo.BranchStock SET Quantity = -1 WHERE BranchCode = 'BR001' AND Sku = 'PKG-0001';
END TRY
BEGIN CATCH
    PRINT CONCAT(ERROR_NUMBER(), ': ', LEFT(ERROR_MESSAGE(), 110));
END CATCH;
BEGIN TRY
    UPDATE TOP (1) dbo.CounterTransactions SET Status = 'Lost';
END TRY
BEGIN CATCH
    PRINT CONCAT(ERROR_NUMBER(), ': ', LEFT(ERROR_MESSAGE(), 110));
END CATCH;
BEGIN TRY
    UPDATE TOP (1) dbo.IdentityVerifications SET DocumentLast4 = '123';
END TRY
BEGIN CATCH
    PRINT CONCAT(ERROR_NUMBER(), ': ', LEFT(ERROR_MESSAGE(), 110));
END CATCH;
