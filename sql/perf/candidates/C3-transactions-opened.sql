-- W2c, W4: the Auditor's list over all branches, and the unpaid arm of the money order report.
CREATE INDEX IX_CounterTransactions_OpenedAt ON dbo.CounterTransactions (OpenedAt) INCLUDE (BranchCode, Status);
