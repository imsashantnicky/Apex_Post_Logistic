-- W2a: the list of a branch sorts all rows of the branch to return 20. The key delivers them in order; the clustered
-- key Id is part of every nonclustered index, so a backward scan gives OpenedAt DESC, Id DESC without a Sort.
-- It starts with BranchCode, so the foreign key index on BranchCode alone becomes redundant.
CREATE INDEX IX_CounterTransactions_BranchCode_OpenedAt ON dbo.CounterTransactions (BranchCode, OpenedAt)
    INCLUDE (Status);
DROP INDEX IX_CounterTransactions_BranchCode ON dbo.CounterTransactions;
