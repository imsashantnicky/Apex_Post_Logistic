-- W1, W5, R1: reading the amounts of a basket needs a key lookup per line. Candidate: cover them.
CREATE INDEX IX_TransactionLines_TransactionId ON dbo.TransactionLines (TransactionId)
    INCLUDE (Sku, UnitPrice, Quantity) WITH (DROP_EXISTING = ON);
