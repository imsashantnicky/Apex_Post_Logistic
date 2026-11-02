-- W1, W3, R4: as C4, for the shipment figures of a report.
CREATE INDEX IX_Shipments_TransactionId ON dbo.Shipments (TransactionId)
    INCLUDE (ZoneCode, Level, WeightGrams, LengthCm, WidthCm, HeightCm, Price) WITH (DROP_EXISTING = ON);
