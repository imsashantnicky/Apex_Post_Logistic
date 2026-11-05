IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101171629_InitialCreate'
)
BEGIN
    CREATE TABLE [AuditEntries] (
        [Id] bigint NOT NULL IDENTITY,
        [At] datetime2 NOT NULL,
        [Event] varchar(100) NOT NULL,
        [StaffId] uniqueidentifier NULL,
        [Role] varchar(20) NULL,
        [BranchCode] varchar(5) NULL,
        [Controller] varchar(100) NOT NULL,
        [Action] varchar(100) NOT NULL,
        [RouteValues] nvarchar(max) NOT NULL,
        [StatusCode] int NOT NULL,
        [DurationMs] bigint NOT NULL,
        [CorrelationId] varchar(64) NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [CreatedBy] varchar(50) NULL,
        [ModifiedAtUtc] datetime2 NULL,
        CONSTRAINT [PK_AuditEntries] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_AuditEntries_DurationMs] CHECK ([DurationMs] >= 0),
        CONSTRAINT [CK_AuditEntries_Role] CHECK ([Role] IS NULL OR [Role] IN ('CounterClerk', 'BranchManager', 'Auditor', 'Administrator')),
        CONSTRAINT [CK_AuditEntries_StatusCode] CHECK ([StatusCode] BETWEEN 100 AND 599)
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101171629_InitialCreate'
)
BEGIN
    CREATE TABLE [Branches] (
        [Code] varchar(5) NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [Region] nvarchar(100) NOT NULL,
        [OpensAt] time NOT NULL,
        [ClosesAt] time NOT NULL,
        [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
        [CreatedAtUtc] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [CreatedBy] varchar(50) NULL,
        [ModifiedAtUtc] datetime2 NULL,
        CONSTRAINT [PK_Branches] PRIMARY KEY ([Code]),
        CONSTRAINT [CK_Branches_Hours] CHECK ([OpensAt] < [ClosesAt])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101171629_InitialCreate'
)
BEGIN
    CREATE TABLE [ProductCategories] (
        [Name] varchar(20) NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [CreatedBy] varchar(50) NULL,
        [ModifiedAtUtc] datetime2 NULL,
        CONSTRAINT [PK_ProductCategories] PRIMARY KEY ([Name]),
        CONSTRAINT [CK_ProductCategories_Name] CHECK ([Name] IN ('Packaging', 'Stationery', 'OfficeSupplies'))
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101171629_InitialCreate'
)
BEGIN
    CREATE TABLE [Roles] (
        [Name] varchar(20) NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [CreatedBy] varchar(50) NULL,
        [ModifiedAtUtc] datetime2 NULL,
        CONSTRAINT [PK_Roles] PRIMARY KEY ([Name]),
        CONSTRAINT [CK_Roles_Name] CHECK ([Name] IN ('CounterClerk', 'BranchManager', 'Auditor', 'Administrator'))
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101171629_InitialCreate'
)
BEGIN
    CREATE TABLE [ShippingZones] (
        [Code] varchar(10) NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [CreatedBy] varchar(50) NULL,
        [ModifiedAtUtc] datetime2 NULL,
        CONSTRAINT [PK_ShippingZones] PRIMARY KEY ([Code])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101171629_InitialCreate'
)
BEGIN
    CREATE TABLE [Counters] (
        [Id] uniqueidentifier NOT NULL,
        [Number] int NOT NULL,
        [BranchCode] varchar(5) NULL,
        [CreatedAtUtc] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [CreatedBy] varchar(50) NULL,
        [ModifiedAtUtc] datetime2 NULL,
        CONSTRAINT [PK_Counters] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Counters_Number] CHECK ([Number] > 0),
        CONSTRAINT [FK_Counters_Branches_BranchCode] FOREIGN KEY ([BranchCode]) REFERENCES [Branches] ([Code]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101171629_InitialCreate'
)
BEGIN
    CREATE TABLE [Products] (
        [Sku] varchar(8) NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [Category] varchar(20) NOT NULL,
        [UnitPrice] decimal(18,2) NOT NULL,
        [TaxCategory] varchar(20) NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [CreatedBy] varchar(50) NULL,
        [ModifiedAtUtc] datetime2 NULL,
        CONSTRAINT [PK_Products] PRIMARY KEY ([Sku]),
        CONSTRAINT [CK_Products_TaxCategory] CHECK ([TaxCategory] IN ('Standard', 'Reduced', 'Exempt')),
        CONSTRAINT [CK_Products_UnitPrice] CHECK ([UnitPrice] > 0),
        CONSTRAINT [FK_Products_ProductCategories_Category] FOREIGN KEY ([Category]) REFERENCES [ProductCategories] ([Name]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101171629_InitialCreate'
)
BEGIN
    CREATE TABLE [Staff] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [Email] varchar(254) NOT NULL,
        [Role] varchar(20) NOT NULL,
        [HomeBranch] varchar(5) NOT NULL,
        [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
        [CreatedAtUtc] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [CreatedBy] varchar(50) NULL,
        [ModifiedAtUtc] datetime2 NULL,
        CONSTRAINT [PK_Staff] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Staff_Role] CHECK ([Role] IN ('CounterClerk', 'BranchManager', 'Auditor', 'Administrator')),
        CONSTRAINT [FK_Staff_Branches_HomeBranch] FOREIGN KEY ([HomeBranch]) REFERENCES [Branches] ([Code]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Staff_Roles_Role] FOREIGN KEY ([Role]) REFERENCES [Roles] ([Name]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101171629_InitialCreate'
)
BEGIN
    CREATE TABLE [ShippingRates] (
        [ZoneCode] varchar(10) NOT NULL,
        [BaseRate] decimal(18,2) NOT NULL,
        [PerKgRate] decimal(18,2) NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [CreatedBy] varchar(50) NULL,
        [ModifiedAtUtc] datetime2 NULL,
        CONSTRAINT [PK_ShippingRates] PRIMARY KEY ([ZoneCode]),
        CONSTRAINT [FK_ShippingRates_ShippingZones_ZoneCode] FOREIGN KEY ([ZoneCode]) REFERENCES [ShippingZones] ([Code]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101171629_InitialCreate'
)
BEGIN
    CREATE TABLE [BranchStock] (
        [BranchCode] varchar(5) NOT NULL,
        [Sku] varchar(8) NOT NULL,
        [Quantity] int NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [CreatedBy] varchar(50) NULL,
        [ModifiedAtUtc] datetime2 NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_BranchStock] PRIMARY KEY ([BranchCode], [Sku]),
        CONSTRAINT [CK_BranchStock_Quantity] CHECK ([Quantity] >= 0),
        CONSTRAINT [FK_BranchStock_Branches_BranchCode] FOREIGN KEY ([BranchCode]) REFERENCES [Branches] ([Code]) ON DELETE NO ACTION,
        CONSTRAINT [FK_BranchStock_Products_Sku] FOREIGN KEY ([Sku]) REFERENCES [Products] ([Sku]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101171629_InitialCreate'
)
BEGIN
    CREATE TABLE [CounterTransactions] (
        [Id] uniqueidentifier NOT NULL,
        [BranchCode] varchar(5) NOT NULL,
        [CounterId] uniqueidentifier NOT NULL,
        [ClerkId] uniqueidentifier NOT NULL,
        [OpenedAt] datetime2 NOT NULL,
        [Status] varchar(20) NOT NULL DEFAULT 'Open',
        [ReversedAt] datetime2 NULL,
        [ReversalManagerId] uniqueidentifier NULL,
        [ReversalReason] nvarchar(500) NULL,
        [CreatedAtUtc] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [CreatedBy] varchar(50) NULL,
        [ModifiedAtUtc] datetime2 NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_CounterTransactions] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_CounterTransactions_Status] CHECK ([Status] IN ('Open', 'Paid', 'Voided', 'Refunded')),
        CONSTRAINT [FK_CounterTransactions_Branches_BranchCode] FOREIGN KEY ([BranchCode]) REFERENCES [Branches] ([Code]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CounterTransactions_Counters_CounterId] FOREIGN KEY ([CounterId]) REFERENCES [Counters] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CounterTransactions_Staff_ClerkId] FOREIGN KEY ([ClerkId]) REFERENCES [Staff] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CounterTransactions_Staff_ReversalManagerId] FOREIGN KEY ([ReversalManagerId]) REFERENCES [Staff] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101171629_InitialCreate'
)
BEGIN
    CREATE TABLE [StockAdjustments] (
        [Id] uniqueidentifier NOT NULL,
        [BranchCode] varchar(5) NOT NULL,
        [Sku] varchar(8) NOT NULL,
        [Change] int NOT NULL,
        [NewQuantity] int NOT NULL,
        [Reason] nvarchar(500) NOT NULL,
        [StaffId] uniqueidentifier NOT NULL,
        [AdjustedAt] datetime2 NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [CreatedBy] varchar(50) NULL,
        [ModifiedAtUtc] datetime2 NULL,
        CONSTRAINT [PK_StockAdjustments] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_StockAdjustments_Change] CHECK ([Change] <> 0),
        CONSTRAINT [CK_StockAdjustments_NewQuantity] CHECK ([NewQuantity] >= 0),
        CONSTRAINT [FK_StockAdjustments_Branches_BranchCode] FOREIGN KEY ([BranchCode]) REFERENCES [Branches] ([Code]) ON DELETE NO ACTION,
        CONSTRAINT [FK_StockAdjustments_Products_Sku] FOREIGN KEY ([Sku]) REFERENCES [Products] ([Sku]) ON DELETE NO ACTION,
        CONSTRAINT [FK_StockAdjustments_Staff_StaffId] FOREIGN KEY ([StaffId]) REFERENCES [Staff] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101171629_InitialCreate'
)
BEGIN
    CREATE TABLE [IdentityVerifications] (
        [Id] uniqueidentifier NOT NULL,
        [TransactionId] uniqueidentifier NOT NULL,
        [DocumentType] varchar(20) NOT NULL,
        [DocumentLast4] varchar(4) NOT NULL,
        [Result] varchar(20) NOT NULL,
        [VerifiedAt] datetime2 NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [CreatedBy] varchar(50) NULL,
        [ModifiedAtUtc] datetime2 NULL,
        CONSTRAINT [PK_IdentityVerifications] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_IdentityVerifications_DocumentType] CHECK ([DocumentType] IN ('Passport', 'NationalIdCard', 'DrivingLicence')),
        CONSTRAINT [CK_IdentityVerifications_Last4] CHECK (LEN([DocumentLast4]) = 4),
        CONSTRAINT [CK_IdentityVerifications_Result] CHECK ([Result] IN ('Passed', 'Failed', 'Pending')),
        CONSTRAINT [FK_IdentityVerifications_CounterTransactions_TransactionId] FOREIGN KEY ([TransactionId]) REFERENCES [CounterTransactions] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101171629_InitialCreate'
)
BEGIN
    CREATE TABLE [MoneyOrders] (
        [TransactionLineId] uniqueidentifier NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [CreatedBy] varchar(50) NULL,
        [ModifiedAtUtc] datetime2 NULL,
        [TransactionId] uniqueidentifier NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [Fee] decimal(18,2) NOT NULL,
        [Sender] nvarchar(100) NOT NULL,
        [Recipient] nvarchar(100) NOT NULL,
        CONSTRAINT [PK_MoneyOrders] PRIMARY KEY ([TransactionLineId]),
        CONSTRAINT [CK_MoneyOrders_Amount] CHECK ([Amount] > 0 AND [Fee] >= 0),
        CONSTRAINT [FK_MoneyOrders_CounterTransactions_TransactionId] FOREIGN KEY ([TransactionId]) REFERENCES [CounterTransactions] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101171629_InitialCreate'
)
BEGIN
    CREATE TABLE [Payments] (
        [TransactionId] uniqueidentifier NOT NULL,
        [PaidAt] datetime2 NOT NULL,
        [Method] varchar(20) NOT NULL,
        [Tax] decimal(18,2) NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [CreatedBy] varchar(50) NULL,
        [ModifiedAtUtc] datetime2 NULL,
        CONSTRAINT [PK_Payments] PRIMARY KEY ([TransactionId]),
        CONSTRAINT [CK_Payments_Method] CHECK ([Method] IN ('Cash', 'Card')),
        CONSTRAINT [CK_Payments_Tax] CHECK ([Tax] >= 0),
        CONSTRAINT [FK_Payments_CounterTransactions_TransactionId] FOREIGN KEY ([TransactionId]) REFERENCES [CounterTransactions] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101171629_InitialCreate'
)
BEGIN
    CREATE TABLE [Shipments] (
        [TransactionLineId] uniqueidentifier NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [CreatedBy] varchar(50) NULL,
        [ModifiedAtUtc] datetime2 NULL,
        [TransactionId] uniqueidentifier NOT NULL,
        [Type] varchar(20) NOT NULL,
        [Level] varchar(20) NOT NULL,
        [WeightGrams] int NOT NULL,
        [LengthCm] decimal(9,2) NOT NULL,
        [WidthCm] decimal(9,2) NOT NULL,
        [HeightCm] decimal(9,2) NOT NULL,
        [RecipientName] nvarchar(100) NOT NULL,
        [RecipientStreet] nvarchar(200) NOT NULL,
        [RecipientCity] nvarchar(100) NOT NULL,
        [RecipientPostalCode] nvarchar(12) NOT NULL,
        [RecipientCountry] nvarchar(100) NOT NULL,
        [ZoneCode] varchar(10) NOT NULL,
        [Price] decimal(18,2) NOT NULL,
        CONSTRAINT [PK_Shipments] PRIMARY KEY ([TransactionLineId]),
        CONSTRAINT [CK_Shipments_Level] CHECK ([Level] IN ('Standard', 'Express', 'Registered')),
        CONSTRAINT [CK_Shipments_Price] CHECK ([Price] > 0),
        CONSTRAINT [CK_Shipments_Type] CHECK ([Type] IN ('Letter', 'Parcel')),
        CONSTRAINT [CK_Shipments_Weight] CHECK ([WeightGrams] > 0 AND [WeightGrams] <= 30000),
        CONSTRAINT [FK_Shipments_CounterTransactions_TransactionId] FOREIGN KEY ([TransactionId]) REFERENCES [CounterTransactions] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_Shipments_ShippingRates_ZoneCode] FOREIGN KEY ([ZoneCode]) REFERENCES [ShippingRates] ([ZoneCode]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101171629_InitialCreate'
)
BEGIN
    CREATE TABLE [TransactionLines] (
        [Id] uniqueidentifier NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [CreatedBy] varchar(50) NULL,
        [ModifiedAtUtc] datetime2 NULL,
        [TransactionId] uniqueidentifier NOT NULL,
        [Sku] varchar(8) NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [UnitPrice] decimal(18,2) NOT NULL,
        [Quantity] int NOT NULL,
        [TaxCategory] varchar(20) NOT NULL,
        CONSTRAINT [PK_TransactionLines] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_TransactionLines_Quantity] CHECK ([Quantity] > 0),
        CONSTRAINT [CK_TransactionLines_TaxCategory] CHECK ([TaxCategory] IN ('Standard', 'Reduced', 'Exempt')),
        CONSTRAINT [CK_TransactionLines_UnitPrice] CHECK ([UnitPrice] > 0),
        CONSTRAINT [FK_TransactionLines_CounterTransactions_TransactionId] FOREIGN KEY ([TransactionId]) REFERENCES [CounterTransactions] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_TransactionLines_Products_Sku] FOREIGN KEY ([Sku]) REFERENCES [Products] ([Sku]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101171629_InitialCreate'
)
BEGIN
    ALTER TABLE [ShippingRates] ADD CONSTRAINT [CK_ShippingRates_Positive] CHECK ([BaseRate] > 0 AND [PerKgRate] > 0)
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101171629_InitialCreate'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Name', N'CreatedAtUtc', N'CreatedBy', N'ModifiedAtUtc') AND [object_id] = OBJECT_ID(N'[ProductCategories]'))
        SET IDENTITY_INSERT [ProductCategories] ON;
    EXEC(N'INSERT INTO [ProductCategories] ([Name], [CreatedAtUtc], [CreatedBy], [ModifiedAtUtc])
    VALUES (''OfficeSupplies'', ''2026-11-01T00:00:00.0000000Z'', NULL, NULL),
    (''Packaging'', ''2026-11-01T00:00:00.0000000Z'', NULL, NULL),
    (''Stationery'', ''2026-11-01T00:00:00.0000000Z'', NULL, NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Name', N'CreatedAtUtc', N'CreatedBy', N'ModifiedAtUtc') AND [object_id] = OBJECT_ID(N'[ProductCategories]'))
        SET IDENTITY_INSERT [ProductCategories] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101171629_InitialCreate'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Name', N'CreatedAtUtc', N'CreatedBy', N'ModifiedAtUtc') AND [object_id] = OBJECT_ID(N'[Roles]'))
        SET IDENTITY_INSERT [Roles] ON;
    EXEC(N'INSERT INTO [Roles] ([Name], [CreatedAtUtc], [CreatedBy], [ModifiedAtUtc])
    VALUES (''Administrator'', ''2026-11-01T00:00:00.0000000Z'', NULL, NULL),
    (''Auditor'', ''2026-11-01T00:00:00.0000000Z'', NULL, NULL),
    (''BranchManager'', ''2026-11-01T00:00:00.0000000Z'', NULL, NULL),
    (''CounterClerk'', ''2026-11-01T00:00:00.0000000Z'', NULL, NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Name', N'CreatedAtUtc', N'CreatedBy', N'ModifiedAtUtc') AND [object_id] = OBJECT_ID(N'[Roles]'))
        SET IDENTITY_INSERT [Roles] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101171629_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_BranchStock_Sku] ON [BranchStock] ([Sku]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101171629_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_Counters_Branch_Number] ON [Counters] ([BranchCode], [Number]) WHERE [BranchCode] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101171629_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_CounterTransactions_BranchCode] ON [CounterTransactions] ([BranchCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101171629_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_CounterTransactions_ClerkId] ON [CounterTransactions] ([ClerkId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101171629_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_CounterTransactions_CounterId] ON [CounterTransactions] ([CounterId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101171629_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_CounterTransactions_ReversalManagerId] ON [CounterTransactions] ([ReversalManagerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101171629_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_IdentityVerifications_TransactionId] ON [IdentityVerifications] ([TransactionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101171629_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_MoneyOrders_TransactionId] ON [MoneyOrders] ([TransactionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101171629_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Products_Category] ON [Products] ([Category]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101171629_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Shipments_TransactionId] ON [Shipments] ([TransactionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101171629_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Shipments_ZoneCode] ON [Shipments] ([ZoneCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101171629_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Staff_HomeBranch] ON [Staff] ([HomeBranch]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101171629_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Staff_Role] ON [Staff] ([Role]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101171629_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UX_Staff_Email] ON [Staff] ([Email]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101171629_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_StockAdjustments_BranchCode] ON [StockAdjustments] ([BranchCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101171629_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_StockAdjustments_Sku] ON [StockAdjustments] ([Sku]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101171629_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_StockAdjustments_StaffId] ON [StockAdjustments] ([StaffId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101171629_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_TransactionLines_Sku] ON [TransactionLines] ([Sku]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101171629_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_TransactionLines_TransactionId] ON [TransactionLines] ([TransactionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101171629_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261101171629_InitialCreate', N'10.0.14');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101172625_AddStaffPasswordHash'
)
BEGIN
    ALTER TABLE [Staff] ADD [PasswordHash] varchar(200) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101172625_AddStaffPasswordHash'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261101172625_AddStaffPasswordHash', N'10.0.14');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101183620_AddBranchAndStaffVersion'
)
BEGIN
    ALTER TABLE [Staff] ADD [Version] int NOT NULL DEFAULT 1;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101183620_AddBranchAndStaffVersion'
)
BEGIN
    ALTER TABLE [Branches] ADD [Version] int NOT NULL DEFAULT 1;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101183620_AddBranchAndStaffVersion'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261101183620_AddBranchAndStaffVersion', N'10.0.14');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102042018_AddStoredProcedures'
)
BEGIN
    EXEC(N'/*
        Paid sales of one branch on one UTC day, split into retail and services: the signature the brief asks for.

        A thin wrapper over usp_DailyBranchSalesRange, so one body computes every sales figure and the measured plan stays
        the plan that runs. The range procedure validates the branch and sniffs the real day, because the day reaches it
        as a parameter, not as a local variable. @BranchCode NULL means all branches.

        Errors: 51004 no date, and those of usp_DailyBranchSalesRange (51003 unknown branch).
    */
    CREATE OR ALTER PROCEDURE dbo.usp_DailyBranchSales
        @BranchCode varchar(5),
        @Date date
    AS
    BEGIN
        SET NOCOUNT ON;

        IF @Date IS NULL
            THROW 51004, ''usp_DailyBranchSales: @Date is required.'', 1;

        EXEC dbo.usp_DailyBranchSalesRange @From = @Date, @To = @Date, @BranchCode = @BranchCode;
    END;
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102042018_AddStoredProcedures'
)
BEGIN
    EXEC(N'/*
        Stock at or below the threshold, lowest first. Backs /Reports/LowStock and the dashboard.

        @BranchCode NULL means all branches. The two shapes are two statements: the choice between them and between
        OPTION (RECOMPILE) is measured in PERFORMANCE.md and decided in ADR-012.
    */
    CREATE OR ALTER PROCEDURE dbo.usp_LowStockReport
        @Threshold int,
        @BranchCode varchar(5) = NULL
    AS
    BEGIN
        SET NOCOUNT ON;

        IF @Threshold IS NULL OR @Threshold < 0
            THROW 51011, ''usp_LowStockReport: @Threshold must be zero or more.'', 1;

        IF @BranchCode IS NOT NULL AND NOT EXISTS (SELECT 1 FROM dbo.Branches WHERE Code = @BranchCode)
            THROW 51012, ''usp_LowStockReport: the branch does not exist.'', 1;

        IF @BranchCode IS NULL
        BEGIN
            SELECT stock.BranchCode, stock.Sku, product.Name AS Product, stock.Quantity
            FROM dbo.BranchStock AS stock
            INNER JOIN dbo.Products AS product ON product.Sku = stock.Sku
            WHERE stock.Quantity <= @Threshold
            ORDER BY stock.Quantity, stock.BranchCode, stock.Sku;
        END
        ELSE
        BEGIN
            SELECT stock.BranchCode, stock.Sku, product.Name AS Product, stock.Quantity
            FROM dbo.BranchStock AS stock
            INNER JOIN dbo.Products AS product ON product.Sku = stock.Sku
            WHERE stock.BranchCode = @BranchCode
              AND stock.Quantity <= @Threshold
            ORDER BY stock.Quantity, stock.BranchCode, stock.Sku;
        END;
    END;
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102042018_AddStoredProcedures'
)
BEGIN
    EXEC(N'/*
        A manual stock correction and its StockAdjustments history row, committed together.

        A single UPDATE computes the new quantity under the row''s lock, so two managers cannot overwrite each other and
        no read-then-write window exists. The first delivery to a branch creates the row. Role and branch ownership stay
        in StockService, which runs before this; this procedure protects the data. See ADR-012 in DESIGN.md.

        @Delta is the signed change, @ChangedBy the staff member who made it. @AdjustedAt defaults to now; the application
        passes its own clock so tests can fix the time.

        Errors: 51021 invalid input, 51022 unknown branch, 51023 unknown product, 51024 not enough stock.
    */
    CREATE OR ALTER PROCEDURE dbo.usp_AdjustStock
        @BranchCode varchar(5),
        @Sku varchar(8),
        @Delta int,
        @Reason nvarchar(500),
        @ChangedBy uniqueidentifier,
        @AdjustedAt datetime2 = NULL,
        @NewQuantity int = NULL OUTPUT
    AS
    BEGIN
        SET NOCOUNT ON;
        SET XACT_ABORT ON;

        IF @BranchCode IS NULL OR @Sku IS NULL OR @ChangedBy IS NULL
            THROW 51021, ''usp_AdjustStock: branch, SKU and @ChangedBy are required.'', 1;

        IF @Delta IS NULL OR @Delta = 0
            THROW 51021, ''usp_AdjustStock: @Delta must not be zero.'', 1;

        IF NULLIF(LTRIM(RTRIM(@Reason)), N'''') IS NULL
            THROW 51021, ''usp_AdjustStock: a reason is required.'', 1;

        IF NOT EXISTS (SELECT 1 FROM dbo.Branches WHERE Code = @BranchCode)
            THROW 51022, ''usp_AdjustStock: the branch does not exist.'', 1;

        IF NOT EXISTS (SELECT 1 FROM dbo.Products WHERE Sku = @Sku)
            THROW 51023, ''usp_AdjustStock: the product does not exist.'', 1;

        DECLARE @At datetime2 = COALESCE(@AdjustedAt, SYSUTCDATETIME());
        DECLARE @CreatedBy varchar(50) = CAST(@ChangedBy AS varchar(36));

        BEGIN TRY
            BEGIN TRANSACTION;

            UPDATE dbo.BranchStock
            SET @NewQuantity = Quantity = Quantity + @Delta,
                ModifiedAtUtc = @At
            WHERE BranchCode = @BranchCode
              AND Sku = @Sku
              AND Quantity + @Delta >= 0;

            IF @@ROWCOUNT = 0
            BEGIN
                -- The row is missing or too low. Only a first delivery may go on, and only by creating the row.
                IF @Delta < 0 OR EXISTS (SELECT 1 FROM dbo.BranchStock WHERE BranchCode = @BranchCode AND Sku = @Sku)
                    THROW 51024, ''usp_AdjustStock: not enough stock.'', 1;

                INSERT dbo.BranchStock (BranchCode, Sku, Quantity, CreatedAtUtc, CreatedBy)
                VALUES (@BranchCode, @Sku, @Delta, @At, @CreatedBy);

                SET @NewQuantity = @Delta;
            END;

            INSERT dbo.StockAdjustments
                (Id, BranchCode, Sku, Change, NewQuantity, Reason, StaffId, AdjustedAt, CreatedAtUtc, CreatedBy)
            VALUES
                (NEWID(), @BranchCode, @Sku, @Delta, @NewQuantity, LTRIM(RTRIM(@Reason)), @ChangedBy, @At, @At,
                 @CreatedBy);

            COMMIT TRANSACTION;
        END TRY
        BEGIN CATCH
            IF XACT_STATE() <> 0
                ROLLBACK TRANSACTION;

            THROW;
        END CATCH;
    END;
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102042018_AddStoredProcedures'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261102042018_AddStoredProcedures', N'10.0.14');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102043309_AddWorkloadIndexes'
)
BEGIN
    DROP INDEX [IX_CounterTransactions_BranchCode] ON [CounterTransactions];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102043309_AddWorkloadIndexes'
)
BEGIN
    CREATE INDEX [IX_Payments_PaidAt] ON [Payments] ([PaidAt]) INCLUDE ([Tax]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102043309_AddWorkloadIndexes'
)
BEGIN
    CREATE INDEX [IX_CounterTransactions_BranchCode_OpenedAt] ON [CounterTransactions] ([BranchCode], [OpenedAt]) INCLUDE ([Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102043309_AddWorkloadIndexes'
)
BEGIN
    CREATE INDEX [IX_CounterTransactions_OpenedAt] ON [CounterTransactions] ([OpenedAt]) INCLUDE ([BranchCode], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102043309_AddWorkloadIndexes'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261102043309_AddWorkloadIndexes', N'10.0.14');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102052538_RequireCounterBranch'
)
BEGIN
    DROP INDEX [UX_Counters_Branch_Number] ON [Counters];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102052538_RequireCounterBranch'
)
BEGIN
    DECLARE @var nvarchar(max);
    SELECT @var = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Counters]') AND [c].[name] = N'BranchCode');
    IF @var IS NOT NULL EXEC(N'ALTER TABLE [Counters] DROP CONSTRAINT ' + @var + ';');
    EXEC(N'UPDATE [Counters] SET [BranchCode] = '''' WHERE [BranchCode] IS NULL');
    ALTER TABLE [Counters] ALTER COLUMN [BranchCode] varchar(5) NOT NULL;
    ALTER TABLE [Counters] ADD DEFAULT '' FOR [BranchCode];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102052538_RequireCounterBranch'
)
BEGIN
    CREATE UNIQUE INDEX [UX_Counters_Branch_Number] ON [Counters] ([BranchCode], [Number]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102052538_RequireCounterBranch'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261102052538_RequireCounterBranch', N'10.0.14');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102130524_MatchBriefProcedureSignatures'
)
BEGIN
    EXEC(N'/*
        Paid sales per UTC day and branch over a period, split into retail and services. Backs /Reports/DailySales and
        the dashboard; usp_DailyBranchSales is the one-day form of the brief. Measured in PERFORMANCE.md under its
        earlier name, usp_DailyBranchSales: the rename changed nothing in the body.

        @From and @To are inclusive days; @BranchCode NULL means all branches. Only transactions still Paid count, so a
        void or a refund takes its sale out of the report. Why this is a procedure and not LINQ: ADR-012 in DESIGN.md.

        The range stays on the PaidAt column and is built from the parameters inside the predicate, so the optimizer sees
        the sniffed days; a local variable would hide them (PERFORMANCE.md, estimate investigation).
    */
    CREATE OR ALTER PROCEDURE dbo.usp_DailyBranchSalesRange
        @From date,
        @To date,
        @BranchCode varchar(5) = NULL
    AS
    BEGIN
        SET NOCOUNT ON;

        IF @From IS NULL OR @To IS NULL
            THROW 51001, ''usp_DailyBranchSalesRange: @From and @To are required.'', 1;

        IF @To < @From
            THROW 51002, ''usp_DailyBranchSalesRange: @To must not be before @From.'', 1;

        IF @BranchCode IS NOT NULL AND NOT EXISTS (SELECT 1 FROM dbo.Branches WHERE Code = @BranchCode)
            THROW 51003, ''usp_DailyBranchSalesRange: the branch does not exist.'', 1;

        -- The period is fixed first: the amounts below then join real rows with real statistics, whatever the range.
        CREATE TABLE #Paid
        (
            Id uniqueidentifier NOT NULL PRIMARY KEY,
            BranchCode varchar(5) NOT NULL,
            SalesDay date NOT NULL,
            Tax decimal(18, 2) NOT NULL
        );

        INSERT #Paid (Id, BranchCode, SalesDay, Tax)
        SELECT tx.Id, tx.BranchCode, CAST(payment.PaidAt AS date), payment.Tax
        FROM dbo.Payments AS payment
        INNER JOIN dbo.CounterTransactions AS tx ON tx.Id = payment.TransactionId
        WHERE payment.PaidAt >= CAST(@From AS datetime2)
          AND payment.PaidAt < DATEADD(DAY, 1, CAST(@To AS datetime2))
          AND tx.Status = ''Paid''
          AND (@BranchCode IS NULL OR tx.BranchCode = @BranchCode);

        WITH Amounts AS
        (
            SELECT line.TransactionId, line.UnitPrice * line.Quantity AS Retail, CAST(0 AS decimal(18, 2)) AS Services
            FROM dbo.TransactionLines AS line
            INNER JOIN #Paid AS paid ON paid.Id = line.TransactionId
            UNION ALL
            SELECT shipment.TransactionId, 0, shipment.Price
            FROM dbo.Shipments AS shipment
            INNER JOIN #Paid AS paid ON paid.Id = shipment.TransactionId
            UNION ALL
            SELECT moneyOrder.TransactionId, 0, moneyOrder.Amount + moneyOrder.Fee
            FROM dbo.MoneyOrders AS moneyOrder
            INNER JOIN #Paid AS paid ON paid.Id = moneyOrder.TransactionId
        ),
        Basket AS
        (
            SELECT TransactionId, SUM(Retail) AS Retail, SUM(Services) AS Services
            FROM Amounts
            GROUP BY TransactionId
        )
        SELECT paid.SalesDay,
               paid.BranchCode,
               COUNT(*) AS Transactions,
               CAST(COALESCE(SUM(basket.Retail), 0) AS decimal(18, 2)) AS Retail,
               CAST(COALESCE(SUM(basket.Services), 0) AS decimal(18, 2)) AS Services,
               CAST(SUM(paid.Tax) AS decimal(18, 2)) AS Tax
        FROM #Paid AS paid
        LEFT JOIN Basket AS basket ON basket.TransactionId = paid.Id
        GROUP BY paid.SalesDay, paid.BranchCode
        ORDER BY paid.SalesDay, paid.BranchCode;
    END;
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102130524_MatchBriefProcedureSignatures'
)
BEGIN
    EXEC(N'/*
        Paid sales of one branch on one UTC day, split into retail and services: the signature the brief asks for.

        A thin wrapper over usp_DailyBranchSalesRange, so one body computes every sales figure and the measured plan stays
        the plan that runs. The range procedure validates the branch and sniffs the real day, because the day reaches it
        as a parameter, not as a local variable. @BranchCode NULL means all branches.

        Errors: 51004 no date, and those of usp_DailyBranchSalesRange (51003 unknown branch).
    */
    CREATE OR ALTER PROCEDURE dbo.usp_DailyBranchSales
        @BranchCode varchar(5),
        @Date date
    AS
    BEGIN
        SET NOCOUNT ON;

        IF @Date IS NULL
            THROW 51004, ''usp_DailyBranchSales: @Date is required.'', 1;

        EXEC dbo.usp_DailyBranchSalesRange @From = @Date, @To = @Date, @BranchCode = @BranchCode;
    END;
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102130524_MatchBriefProcedureSignatures'
)
BEGIN
    EXEC(N'/*
        A manual stock correction and its StockAdjustments history row, committed together.

        A single UPDATE computes the new quantity under the row''s lock, so two managers cannot overwrite each other and
        no read-then-write window exists. The first delivery to a branch creates the row. Role and branch ownership stay
        in StockService, which runs before this; this procedure protects the data. See ADR-012 in DESIGN.md.

        @Delta is the signed change, @ChangedBy the staff member who made it. @AdjustedAt defaults to now; the application
        passes its own clock so tests can fix the time.

        Errors: 51021 invalid input, 51022 unknown branch, 51023 unknown product, 51024 not enough stock.
    */
    CREATE OR ALTER PROCEDURE dbo.usp_AdjustStock
        @BranchCode varchar(5),
        @Sku varchar(8),
        @Delta int,
        @Reason nvarchar(500),
        @ChangedBy uniqueidentifier,
        @AdjustedAt datetime2 = NULL,
        @NewQuantity int = NULL OUTPUT
    AS
    BEGIN
        SET NOCOUNT ON;
        SET XACT_ABORT ON;

        IF @BranchCode IS NULL OR @Sku IS NULL OR @ChangedBy IS NULL
            THROW 51021, ''usp_AdjustStock: branch, SKU and @ChangedBy are required.'', 1;

        IF @Delta IS NULL OR @Delta = 0
            THROW 51021, ''usp_AdjustStock: @Delta must not be zero.'', 1;

        IF NULLIF(LTRIM(RTRIM(@Reason)), N'''') IS NULL
            THROW 51021, ''usp_AdjustStock: a reason is required.'', 1;

        IF NOT EXISTS (SELECT 1 FROM dbo.Branches WHERE Code = @BranchCode)
            THROW 51022, ''usp_AdjustStock: the branch does not exist.'', 1;

        IF NOT EXISTS (SELECT 1 FROM dbo.Products WHERE Sku = @Sku)
            THROW 51023, ''usp_AdjustStock: the product does not exist.'', 1;

        DECLARE @At datetime2 = COALESCE(@AdjustedAt, SYSUTCDATETIME());
        DECLARE @CreatedBy varchar(50) = CAST(@ChangedBy AS varchar(36));

        BEGIN TRY
            BEGIN TRANSACTION;

            UPDATE dbo.BranchStock
            SET @NewQuantity = Quantity = Quantity + @Delta,
                ModifiedAtUtc = @At
            WHERE BranchCode = @BranchCode
              AND Sku = @Sku
              AND Quantity + @Delta >= 0;

            IF @@ROWCOUNT = 0
            BEGIN
                -- The row is missing or too low. Only a first delivery may go on, and only by creating the row.
                IF @Delta < 0 OR EXISTS (SELECT 1 FROM dbo.BranchStock WHERE BranchCode = @BranchCode AND Sku = @Sku)
                    THROW 51024, ''usp_AdjustStock: not enough stock.'', 1;

                INSERT dbo.BranchStock (BranchCode, Sku, Quantity, CreatedAtUtc, CreatedBy)
                VALUES (@BranchCode, @Sku, @Delta, @At, @CreatedBy);

                SET @NewQuantity = @Delta;
            END;

            INSERT dbo.StockAdjustments
                (Id, BranchCode, Sku, Change, NewQuantity, Reason, StaffId, AdjustedAt, CreatedAtUtc, CreatedBy)
            VALUES
                (NEWID(), @BranchCode, @Sku, @Delta, @NewQuantity, LTRIM(RTRIM(@Reason)), @ChangedBy, @At, @At,
                 @CreatedBy);

            COMMIT TRANSACTION;
        END TRY
        BEGIN CATCH
            IF XACT_STATE() <> 0
                ROLLBACK TRANSACTION;

            THROW;
        END CATCH;
    END;
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102130524_MatchBriefProcedureSignatures'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261102130524_MatchBriefProcedureSignatures', N'10.0.14');
END;

COMMIT;
GO

