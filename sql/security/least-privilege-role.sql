/*
    The database role of the application: what the running web application needs and nothing more. It creates no
    login and holds no secret. Migrations run under a separate deployment login (db_owner), never this one.
    PERFORMANCE.md explains the table. LeastPrivilegeTests runs the real application on this role.

        sqlcmd -S <server> -E -C -I -d ApexPostDb -b -i sql/security/least-privilege-role.sql
*/
IF DATABASE_PRINCIPAL_ID('apexpost_app') IS NULL
    CREATE ROLE apexpost_app AUTHORIZATION dbo;
GO
-- Reads: every screen, report and query service. The migration history is left out: the application never migrates.
GRANT SELECT ON dbo.AuditEntries TO apexpost_app;
GRANT SELECT ON dbo.Branches TO apexpost_app;
GRANT SELECT ON dbo.BranchStock TO apexpost_app;
GRANT SELECT ON dbo.Counters TO apexpost_app;
GRANT SELECT ON dbo.CounterTransactions TO apexpost_app;
GRANT SELECT ON dbo.IdentityVerifications TO apexpost_app;
GRANT SELECT ON dbo.MoneyOrders TO apexpost_app;
GRANT SELECT ON dbo.Payments TO apexpost_app;
GRANT SELECT ON dbo.ProductCategories TO apexpost_app;
GRANT SELECT ON dbo.Products TO apexpost_app;
GRANT SELECT ON dbo.Roles TO apexpost_app;
GRANT SELECT ON dbo.Shipments TO apexpost_app;
GRANT SELECT ON dbo.ShippingRates TO apexpost_app;
GRANT SELECT ON dbo.ShippingZones TO apexpost_app;
GRANT SELECT ON dbo.Staff TO apexpost_app;
GRANT SELECT ON dbo.StockAdjustments TO apexpost_app;
GRANT SELECT ON dbo.TransactionLines TO apexpost_app;
GO
-- Writes: only what a use case changes. No line, payment or check is ever edited or removed: reversals add to history.
GRANT UPDATE ON dbo.Branches TO apexpost_app;
GRANT INSERT, DELETE ON dbo.Counters TO apexpost_app;
GRANT INSERT, UPDATE ON dbo.Staff TO apexpost_app;
GRANT UPDATE ON dbo.Products TO apexpost_app;
GRANT UPDATE ON dbo.BranchStock TO apexpost_app;
GRANT INSERT, UPDATE ON dbo.CounterTransactions TO apexpost_app;
GRANT INSERT ON dbo.TransactionLines TO apexpost_app;
GRANT INSERT ON dbo.Shipments TO apexpost_app;
GRANT INSERT ON dbo.MoneyOrders TO apexpost_app;
GRANT INSERT ON dbo.IdentityVerifications TO apexpost_app;
GRANT INSERT ON dbo.Payments TO apexpost_app;
GRANT INSERT ON dbo.AuditEntries TO apexpost_app;
GO
-- Ownership chaining lets usp_AdjustStock insert stock and history rows without a grant on those operations.
GRANT EXECUTE ON OBJECT::dbo.usp_DailyBranchSales TO apexpost_app;
GRANT EXECUTE ON OBJECT::dbo.usp_DailyBranchSalesRange TO apexpost_app;
GRANT EXECUTE ON OBJECT::dbo.usp_LowStockReport TO apexpost_app;
GRANT EXECUTE ON OBJECT::dbo.usp_AdjustStock TO apexpost_app;
GO
-- History is append-only, even if someone later adds a broader grant.
DENY UPDATE, DELETE ON dbo.AuditEntries TO apexpost_app;
DENY INSERT, UPDATE, DELETE ON dbo.StockAdjustments TO apexpost_app;
GO
