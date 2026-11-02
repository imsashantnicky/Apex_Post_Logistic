-- W6: low stock is the textbook filtered index, on a table of 192 rows. Measured to find out if it pays at all.
CREATE INDEX IX_BranchStock_Low ON dbo.BranchStock (BranchCode, Quantity) INCLUDE (Sku) WHERE Quantity <= 5;
