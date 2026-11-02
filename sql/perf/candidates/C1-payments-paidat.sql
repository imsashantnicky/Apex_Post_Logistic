-- W1, W3, W4, W5, W7, R1, R4, R5: every period report filters Payments on PaidAt and today scans the table.
-- INCLUDE (Tax): usp_DailyBranchSales needs it, and it saves a key lookup per payment.
CREATE INDEX IX_Payments_PaidAt ON dbo.Payments (PaidAt) INCLUDE (Tax);
