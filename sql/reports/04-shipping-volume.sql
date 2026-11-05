/*
    Shipping volume per zone and service level. Every zone appears, even one without shipments in the period; a level
    only appears once it has shipments, unless @MinShipments is 0.
    Chargeable weight is the larger of the actual weight and the volumetric weight (L x W x H / 5000), rule 8 of the
    domain. Parameters: @From, @To (inclusive days), @BranchCode (NULL = all), @MinShipments.
*/
DECLARE @From date = DATEADD(DAY, -14, CAST(SYSUTCDATETIME() AS date));
DECLARE @To date = CAST(SYSUTCDATETIME() AS date);
DECLARE @BranchCode varchar(5) = NULL;
DECLARE @MinShipments int = 1;
-- query
WITH Shipped AS
(
    SELECT shipment.ZoneCode,
           shipment.Level,
           shipment.Price,
           CASE WHEN shipment.WeightGrams / 1000.0
                     > shipment.LengthCm * shipment.WidthCm * shipment.HeightCm / 5000
                THEN shipment.WeightGrams / 1000.0
                ELSE shipment.LengthCm * shipment.WidthCm * shipment.HeightCm / 5000
           END AS ChargeableKg
    FROM dbo.Payments AS payment
    INNER JOIN dbo.CounterTransactions AS tx ON tx.Id = payment.TransactionId
    INNER JOIN dbo.Shipments AS shipment ON shipment.TransactionId = tx.Id
    WHERE payment.PaidAt >= CAST(@From AS datetime2)
      AND payment.PaidAt < DATEADD(DAY, 1, CAST(@To AS datetime2))
      AND tx.Status = 'Paid'
      AND (@BranchCode IS NULL OR tx.BranchCode = @BranchCode)
)
SELECT zone.Code AS ZoneCode,
       zone.Name AS ZoneName,
       shipped.Level,
       COUNT(shipped.ZoneCode) AS Shipments,
       CAST(COALESCE(SUM(shipped.ChargeableKg), 0) AS decimal(18, 2)) AS ChargeableKg,
       COALESCE(SUM(shipped.Price), 0) AS Revenue
FROM dbo.ShippingZones AS zone
LEFT JOIN Shipped AS shipped ON shipped.ZoneCode = zone.Code
GROUP BY zone.Code, zone.Name, shipped.Level
HAVING COUNT(shipped.ZoneCode) >= @MinShipments
ORDER BY zone.Code, shipped.Level;
