/*
    Multiplies the demo data for performance tests, on a development database only. All data is fictional.
    Target: about 200k transactions, 600k lines (533k retail + 50k shipments + 20k money orders), 200k payments.

    Run it once, after the application has seeded the database:
        sqlcmd -S localhost -E -C -d ApexPostDb -b -i sql/perf/volume-seed.sql

    Set-based: every value is derived from the row number n, so there are no loops and the mix repeats (ids and the
    end date do not). Rows follow the domain rules (weight limits, fee, tax, verification above 300), so EF Core can
    load them. They end yesterday, so the branches without sales today stay that way. BranchStock is left as it is.
    Each new row has CreatedBy = 'volume-seed', which also stops a second run. One transaction: all rows or none.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @Transactions int = 200000;
DECLARE @Days int = 365;
DECLARE @Marker varchar(50) = 'volume-seed';
DECLARE @Today datetime2 = CAST(CAST(SYSUTCDATETIME() AS date) AS datetime2);

-- The same values as appsettings.json.
DECLARE @StandardTax decimal(5, 2) = 0.20;
DECLARE @ReducedTax decimal(5, 2) = 0.05;
DECLARE @ExpressMultiplier decimal(5, 2) = 1.60;
DECLARE @RegisteredMultiplier decimal(5, 2) = 1.30;

IF NOT EXISTS (SELECT 1 FROM dbo.Branches)
    THROW 50001, 'The database has no branches. Start the application in Development once, then run this script.', 1;

IF EXISTS (SELECT 1 FROM dbo.CounterTransactions WHERE CreatedBy = @Marker)
    THROW 50002, 'The volume data is already there. This script runs once.', 1;

-- Numbered lookups, so that n can pick a row with n % count + 1.
SELECT ROW_NUMBER() OVER (ORDER BY Code) AS rn, Code, OpensAt,
       DATEDIFF(MINUTE, OpensAt, ClosesAt) - 60 AS OpenMinutes
INTO #Branch
FROM dbo.Branches
WHERE IsActive = 1;

SELECT ROW_NUMBER() OVER (PARTITION BY HomeBranch ORDER BY Email) AS rn,
       COUNT(*) OVER (PARTITION BY HomeBranch) AS total, HomeBranch, Id
INTO #Clerk
FROM dbo.Staff
WHERE Role = 'CounterClerk' AND IsActive = 1;

SELECT ROW_NUMBER() OVER (PARTITION BY BranchCode ORDER BY Number) AS rn,
       COUNT(*) OVER (PARTITION BY BranchCode) AS total, BranchCode, Id
INTO #Counter
FROM dbo.Counters;

SELECT ROW_NUMBER() OVER (ORDER BY Sku) AS rn, Sku, Name, UnitPrice, TaxCategory
INTO #Product
FROM dbo.Products;

SELECT ROW_NUMBER() OVER (ORDER BY ZoneCode) AS rn, ZoneCode, BaseRate, PerKgRate
INTO #Zone
FROM dbo.ShippingRates;

DECLARE @BranchCount int = (SELECT COUNT(*) FROM #Branch);
DECLARE @ProductCount int = (SELECT COUNT(*) FROM #Product);
DECLARE @ZoneCount int = (SELECT COUNT(*) FROM #Zone);

-- One row per new transaction; the inserts below all start from this table.
WITH Digits AS (SELECT d FROM (VALUES (0), (1), (2), (3), (4), (5), (6), (7), (8), (9)) AS v (d)),
Numbers AS
(
    SELECT TOP (@Transactions) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS n
    FROM Digits AS a CROSS JOIN Digits AS b CROSS JOIN Digits AS c
    CROSS JOIN Digits AS d CROSS JOIN Digits AS e CROSS JOIN Digits AS f
)
SELECT numbers.n,
       NEWID() AS Id,
       branch.Code AS BranchCode,
       counter.Id AS CounterId,
       clerk.Id AS ClerkId,
       manager.Id AS ManagerId,
       DATEADD(MINUTE,
           DATEDIFF(MINUTE, '00:00', branch.OpensAt) + (numbers.n * 7) % branch.OpenMinutes,
           DATEADD(DAY, -(1 + numbers.n % @Days), @Today)) AS OpenedAt,
       CASE numbers.n % 20 WHEN 7 THEN 'Voided' WHEN 13 THEN 'Refunded' ELSE 'Paid' END AS Status
INTO #Tx
FROM Numbers AS numbers
JOIN #Branch AS branch ON branch.rn = numbers.n % @BranchCount + 1
CROSS APPLY
(
    SELECT Id FROM #Clerk
    WHERE HomeBranch = branch.Code AND rn = (numbers.n / @BranchCount) % total + 1
) AS clerk
CROSS APPLY
(
    SELECT Id FROM #Counter
    WHERE BranchCode = branch.Code AND rn = (numbers.n / @BranchCount) % total + 1
) AS counter
CROSS APPLY
(
    SELECT TOP (1) Id FROM dbo.Staff
    WHERE HomeBranch = branch.Code AND Role = 'BranchManager'
) AS manager;

CREATE UNIQUE CLUSTERED INDEX CX_Tx ON #Tx (n);

BEGIN TRANSACTION;

INSERT dbo.CounterTransactions
    (Id, BranchCode, CounterId, ClerkId, OpenedAt, Status, ReversedAt, ReversalManagerId, ReversalReason, CreatedBy)
SELECT Id, BranchCode, CounterId, ClerkId, OpenedAt, Status,
       CASE Status
           WHEN 'Voided' THEN DATEADD(MINUTE, 40, OpenedAt)
           WHEN 'Refunded' THEN DATEADD(MINUTE, 50, OpenedAt)
       END,
       CASE WHEN Status <> 'Paid' THEN ManagerId END,
       CASE Status
           WHEN 'Voided' THEN N'Customer changed their mind at the counter.'
           WHEN 'Refunded' THEN N'Damaged item returned with receipt.'
       END,
       @Marker
FROM #Tx;

-- Retail: two items in one basket of three, three items in the others.
INSERT dbo.TransactionLines (Id, TransactionId, Sku, Name, UnitPrice, Quantity, TaxCategory, CreatedBy)
SELECT NEWID(), tx.Id, product.Sku, product.Name, product.UnitPrice, 1 + (tx.n + slot.k) % 3, product.TaxCategory, @Marker
FROM #Tx AS tx
JOIN (VALUES (1), (2), (3)) AS slot (k) ON slot.k <= CASE WHEN tx.n % 3 = 0 THEN 2 ELSE 3 END
JOIN #Product AS product ON product.rn = (tx.n * 7 + slot.k * 11) % @ProductCount + 1;

-- Shipments: every fourth transaction. One in three is a letter; parcels stay under the 10 kg limit of Registered.
INSERT dbo.Shipments
    (TransactionLineId, TransactionId, Type, Level, WeightGrams, LengthCm, WidthCm, HeightCm,
     RecipientName, RecipientStreet, RecipientCity, RecipientPostalCode, RecipientCountry, ZoneCode, Price, CreatedBy)
SELECT NEWID(), tx.Id, parcel.Type, parcel.Level, parcel.Grams, parcel.LengthCm, parcel.WidthCm, parcel.HeightCm,
       recipient.Name, recipient.Street, recipient.City, recipient.PostalCode, recipient.Country,
       zone.ZoneCode,
       CAST(ROUND((zone.BaseRate + zone.PerKgRate * charged.Kg) * parcel.Multiplier, 2) AS decimal(18, 2)),
       @Marker
FROM #Tx AS tx
CROSS APPLY
(
    SELECT CASE WHEN tx.n % 12 = 0 THEN 'Letter' ELSE 'Parcel' END AS Type,
           CASE WHEN tx.n % 12 = 0 THEN 'Standard'
                ELSE CASE tx.n / 12 % 3 WHEN 1 THEN 'Express' WHEN 2 THEN 'Registered' ELSE 'Standard' END
           END AS Level,
           CASE WHEN tx.n % 12 = 0 THEN 1.00
                ELSE CASE tx.n / 12 % 3 WHEN 1 THEN @ExpressMultiplier WHEN 2 THEN @RegisteredMultiplier ELSE 1.00 END
           END AS Multiplier,
           CASE WHEN tx.n % 12 = 0 THEN 20 + tx.n % 480 ELSE 500 + tx.n % 9500 END AS Grams,
           CAST(CASE WHEN tx.n % 12 = 0 THEN 24 ELSE 20 + tx.n % 30 END AS decimal(9, 2)) AS LengthCm,
           CAST(CASE WHEN tx.n % 12 = 0 THEN 16 ELSE 15 END AS decimal(9, 2)) AS WidthCm,
           CAST(CASE WHEN tx.n % 12 = 0 THEN 0.5 ELSE 10 END AS decimal(9, 2)) AS HeightCm
) AS parcel
CROSS APPLY
(
    SELECT CASE WHEN parcel.Grams / 1000.0 > parcel.LengthCm * parcel.WidthCm * parcel.HeightCm / 5000
                THEN parcel.Grams / 1000.0
                ELSE parcel.LengthCm * parcel.WidthCm * parcel.HeightCm / 5000
           END AS Kg
) AS charged
JOIN #Zone AS zone ON zone.rn = (tx.n / 4) % @ZoneCount + 1
JOIN
(
    VALUES
        (0, N'Hannah Reid', N'12 Mill Lane', N'Bristol', N'BS1 4DJ', N'United Kingdom'),
        (1, N'Marco Bianchi', N'Via Roma 8', N'Milan', N'20121', N'Italy'),
        (2, N'Priya Nair', N'44 Lake Road', N'Toronto', N'M5V 2T6', N'Canada'),
        (3, N'Kenji Sato', N'3-1 Marunouchi', N'Tokyo', N'100-0005', N'Japan')
) AS recipient (rn, Name, Street, City, PostalCode, Country) ON recipient.rn = (tx.n / 4) % 4
WHERE tx.n % 4 = 0;

-- Money orders: one transaction in ten. One in five of them is above the verification threshold of 300.
INSERT dbo.MoneyOrders (TransactionLineId, TransactionId, Amount, Fee, Sender, Recipient, CreatedBy)
SELECT NEWID(), tx.Id, money.Amount,
       CASE WHEN ROUND(money.Amount * 0.02, 2) < 2 THEN 2 ELSE ROUND(money.Amount * 0.02, 2) END,
       N'Sender ' + CAST(tx.n AS nvarchar(10)),
       N'Recipient ' + CAST(tx.n AS nvarchar(10)),
       @Marker
FROM #Tx AS tx
CROSS APPLY
(
    SELECT CAST(CASE WHEN tx.n % 50 = 1 THEN 301 + (tx.n * 37) % 1200 ELSE 20 + (tx.n * 37) % 280 END
                AS decimal(18, 2)) AS Amount
) AS money
WHERE tx.n % 10 = 1;

-- A passed check for every money order above the threshold. Only 4 characters of a fictional number are kept.
INSERT dbo.IdentityVerifications (Id, TransactionId, DocumentType, DocumentLast4, Result, VerifiedAt, CreatedBy)
SELECT NEWID(), tx.Id,
       CASE tx.n % 3 WHEN 0 THEN 'Passport' WHEN 1 THEN 'NationalIdCard' ELSE 'DrivingLicence' END,
       RIGHT('000' + CAST(tx.n % 10000 AS varchar(4)), 4),
       'Passed', tx.OpenedAt, @Marker
FROM #Tx AS tx
WHERE tx.n % 50 = 1;

-- Tax as the domain freezes it at payment: per line, rounded; money orders are exempt.
WITH LineTax AS
(
    SELECT TransactionId,
           ROUND(UnitPrice * Quantity * CASE TaxCategory
               WHEN 'Standard' THEN @StandardTax WHEN 'Reduced' THEN @ReducedTax ELSE 0 END, 2) AS Tax
    FROM dbo.TransactionLines
    WHERE CreatedBy = @Marker
    UNION ALL
    SELECT TransactionId, ROUND(Price * @StandardTax, 2)
    FROM dbo.Shipments
    WHERE CreatedBy = @Marker
),
BasketTax AS
(
    SELECT TransactionId, SUM(Tax) AS Tax FROM LineTax GROUP BY TransactionId
)
INSERT dbo.Payments (TransactionId, Method, Tax, PaidAt, CreatedBy)
SELECT tx.Id, CASE tx.n % 2 WHEN 0 THEN 'Card' ELSE 'Cash' END, basket.Tax, DATEADD(MINUTE, 4, tx.OpenedAt), @Marker
FROM #Tx AS tx
JOIN BasketTax AS basket ON basket.TransactionId = tx.Id;

COMMIT TRANSACTION;

SELECT 'CounterTransactions' AS TableName, COUNT(*) AS Rows FROM dbo.CounterTransactions
UNION ALL SELECT 'TransactionLines', COUNT(*) FROM dbo.TransactionLines
UNION ALL SELECT 'Shipments', COUNT(*) FROM dbo.Shipments
UNION ALL SELECT 'MoneyOrders', COUNT(*) FROM dbo.MoneyOrders
UNION ALL SELECT 'Payments', COUNT(*) FROM dbo.Payments
UNION ALL SELECT 'IdentityVerifications', COUNT(*) FROM dbo.IdentityVerifications;
