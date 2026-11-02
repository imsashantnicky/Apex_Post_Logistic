/*
    The write side of a sale, 1000 times: a header, its payment, three retail lines; every fourth sale a shipment. Only
    the indexes differ between phases. Rolled back, so the row counts stay the same; the log bytes are read first.
*/
SET NOCOUNT ON;
DECLARE @Clerk uniqueidentifier, @Counter uniqueidentifier;
SELECT TOP (1) @Clerk = ClerkId, @Counter = CounterId FROM dbo.CounterTransactions WHERE BranchCode = 'BR001';

SELECT TOP (1000) NEWID() AS Id, ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS n,
       DATEADD(SECOND, -(ROW_NUMBER() OVER (ORDER BY (SELECT NULL))), SYSUTCDATETIME()) AS At
INTO #Sale
FROM sys.all_objects;

BEGIN TRANSACTION;

INSERT dbo.CounterTransactions (Id, BranchCode, CounterId, ClerkId, OpenedAt, Status, CreatedBy)
SELECT Id, 'BR001', @Counter, @Clerk, At, 'Paid', 'write-test' FROM #Sale;

INSERT dbo.Payments (TransactionId, Method, Tax, PaidAt, CreatedBy)
SELECT Id, 'Card', 1.20, DATEADD(MINUTE, 4, At), 'write-test' FROM #Sale;

INSERT dbo.TransactionLines (Id, TransactionId, Sku, Name, UnitPrice, Quantity, TaxCategory, CreatedBy)
SELECT NEWID(), sale.Id, product.Sku, product.Name, product.UnitPrice, 1, product.TaxCategory, 'write-test'
FROM #Sale AS sale
CROSS APPLY (SELECT TOP (3) Sku, Name, UnitPrice, TaxCategory FROM dbo.Products ORDER BY Sku) AS product;

INSERT dbo.Shipments
    (TransactionLineId, TransactionId, Type, Level, WeightGrams, LengthCm, WidthCm, HeightCm,
     RecipientName, RecipientStreet, RecipientCity, RecipientPostalCode, RecipientCountry, ZoneCode, Price, CreatedBy)
SELECT NEWID(), Id, 'Parcel', 'Standard', 1000, 20, 15, 10, N'Test', N'Street', N'City', N'1000', N'Country', 'Z01', 9.50,
       'write-test'
FROM #Sale
WHERE n % 4 = 0;

SELECT transactions.database_transaction_log_bytes_used AS LogBytes
FROM sys.dm_tran_database_transactions AS transactions
JOIN sys.dm_tran_current_transaction AS currentTransaction
    ON currentTransaction.transaction_id = transactions.transaction_id
WHERE transactions.database_id = DB_ID();

ROLLBACK TRANSACTION;
DROP TABLE #Sale;
