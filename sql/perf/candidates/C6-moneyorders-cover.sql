-- W1, R5: as C4, for the money order figures of a report.
CREATE INDEX IX_MoneyOrders_TransactionId ON dbo.MoneyOrders (TransactionId) INCLUDE (Amount, Fee)
    WITH (DROP_EXISTING = ON);
