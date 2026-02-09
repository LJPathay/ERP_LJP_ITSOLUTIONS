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
GO

CREATE TABLE [Categories] (
    [CategoryID] int NOT NULL IDENTITY,
    [CategoryName] nvarchar(100) NOT NULL,
    CONSTRAINT [PK_Categories] PRIMARY KEY ([CategoryID])
);
GO

CREATE TABLE [Customers] (
    [CustomerID] int NOT NULL IDENTITY,
    [FullName] nvarchar(100) NOT NULL,
    [PhoneNumber] nvarchar(20) NULL,
    [Points] int NOT NULL,
    CONSTRAINT [PK_Customers] PRIMARY KEY ([CustomerID])
);
GO

CREATE TABLE [Promotions] (
    [PromotionID] int NOT NULL IDENTITY,
    [PromotionName] nvarchar(100) NOT NULL,
    [DiscountType] nvarchar(max) NOT NULL,
    [DiscountValue] decimal(18,2) NOT NULL,
    [StartDate] datetime2 NOT NULL,
    [EndDate] datetime2 NOT NULL,
    [IsActive] bit NOT NULL,
    CONSTRAINT [PK_Promotions] PRIMARY KEY ([PromotionID])
);
GO

CREATE TABLE [Roles] (
    [RoleID] int NOT NULL IDENTITY,
    [RoleName] nvarchar(50) NOT NULL,
    CONSTRAINT [PK_Roles] PRIMARY KEY ([RoleID])
);
GO

CREATE TABLE [Products] (
    [ProductID] int NOT NULL IDENTITY,
    [ProductName] nvarchar(100) NOT NULL,
    [CategoryID] int NOT NULL,
    [Price] decimal(18,2) NOT NULL,
    [StockQuantity] int NOT NULL,
    [ImageURL] nvarchar(255) NULL,
    [IsAvailable] bit NOT NULL,
    CONSTRAINT [PK_Products] PRIMARY KEY ([ProductID]),
    CONSTRAINT [FK_Products_Categories_CategoryID] FOREIGN KEY ([CategoryID]) REFERENCES [Categories] ([CategoryID]) ON DELETE CASCADE
);
GO

CREATE TABLE [Users] (
    [UserID] uniqueidentifier NOT NULL,
    [FullName] nvarchar(100) NOT NULL,
    [Username] nvarchar(50) NOT NULL,
    [Email] nvarchar(max) NULL,
    [Password] nvarchar(max) NULL,
    [RoleID] int NOT NULL,
    [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
    [CreatedAt] datetime2 NOT NULL DEFAULT (GETDATE()),
    CONSTRAINT [PK_Users] PRIMARY KEY ([UserID]),
    CONSTRAINT [FK_Users_Roles_RoleID] FOREIGN KEY ([RoleID]) REFERENCES [Roles] ([RoleID]) ON DELETE CASCADE
);
GO

CREATE TABLE [InventoryLogs] (
    [LogID] int NOT NULL IDENTITY,
    [ProductID] int NOT NULL,
    [QuantityChange] int NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_InventoryLogs] PRIMARY KEY ([LogID]),
    CONSTRAINT [FK_InventoryLogs_Products_ProductID] FOREIGN KEY ([ProductID]) REFERENCES [Products] ([ProductID]) ON DELETE CASCADE
);
GO

CREATE TABLE [AuditLogs] (
    [AuditID] int NOT NULL IDENTITY,
    [UserID] uniqueidentifier NULL,
    [Action] nvarchar(max) NOT NULL,
    [Timestamp] datetime2 NOT NULL,
    CONSTRAINT [PK_AuditLogs] PRIMARY KEY ([AuditID]),
    CONSTRAINT [FK_AuditLogs_Users_UserID] FOREIGN KEY ([UserID]) REFERENCES [Users] ([UserID])
);
GO

CREATE TABLE [Orders] (
    [OrderID] uniqueidentifier NOT NULL,
    [OrderDate] datetime2 NOT NULL DEFAULT (GETDATE()),
    [CashierID] uniqueidentifier NOT NULL,
    [CustomerID] int NULL,
    [PromotionID] int NULL,
    [TotalAmount] decimal(18,2) NOT NULL,
    [DiscountAmount] decimal(18,2) NOT NULL,
    [FinalAmount] decimal(18,2) NOT NULL,
    [PaymentStatus] nvarchar(20) NOT NULL,
    [PaymentMethod] nvarchar(50) NOT NULL,
    CONSTRAINT [PK_Orders] PRIMARY KEY ([OrderID]),
    CONSTRAINT [FK_Orders_Customers_CustomerID] FOREIGN KEY ([CustomerID]) REFERENCES [Customers] ([CustomerID]),
    CONSTRAINT [FK_Orders_Promotions_PromotionID] FOREIGN KEY ([PromotionID]) REFERENCES [Promotions] ([PromotionID]),
    CONSTRAINT [FK_Orders_Users_CashierID] FOREIGN KEY ([CashierID]) REFERENCES [Users] ([UserID]) ON DELETE NO ACTION
);
GO

CREATE TABLE [OrderDetails] (
    [OrderDetailID] int NOT NULL IDENTITY,
    [OrderID] uniqueidentifier NOT NULL,
    [ProductID] int NOT NULL,
    [Quantity] int NOT NULL,
    [UnitPrice] decimal(18,2) NOT NULL,
    [Subtotal] decimal(18,2) NOT NULL,
    CONSTRAINT [PK_OrderDetails] PRIMARY KEY ([OrderDetailID]),
    CONSTRAINT [FK_OrderDetails_Orders_OrderID] FOREIGN KEY ([OrderID]) REFERENCES [Orders] ([OrderID]) ON DELETE CASCADE,
    CONSTRAINT [FK_OrderDetails_Products_ProductID] FOREIGN KEY ([ProductID]) REFERENCES [Products] ([ProductID]) ON DELETE CASCADE
);
GO

CREATE TABLE [Payments] (
    [PaymentID] int NOT NULL IDENTITY,
    [OrderID] uniqueidentifier NOT NULL,
    [AmountPaid] decimal(18,2) NOT NULL,
    [PaymentMethod] nvarchar(50) NOT NULL,
    [ReferenceNumber] nvarchar(100) NULL,
    [PaymentDate] datetime2 NOT NULL,
    [PaymentStatus] nvarchar(20) NOT NULL,
    CONSTRAINT [PK_Payments] PRIMARY KEY ([PaymentID]),
    CONSTRAINT [FK_Payments_Orders_OrderID] FOREIGN KEY ([OrderID]) REFERENCES [Orders] ([OrderID]) ON DELETE CASCADE
);
GO

CREATE INDEX [IX_AuditLogs_UserID] ON [AuditLogs] ([UserID]);
GO

CREATE INDEX [IX_InventoryLogs_ProductID] ON [InventoryLogs] ([ProductID]);
GO

CREATE INDEX [IX_OrderDetails_OrderID] ON [OrderDetails] ([OrderID]);
GO

CREATE INDEX [IX_OrderDetails_ProductID] ON [OrderDetails] ([ProductID]);
GO

CREATE INDEX [IX_Orders_CashierID] ON [Orders] ([CashierID]);
GO

CREATE INDEX [IX_Orders_CustomerID] ON [Orders] ([CustomerID]);
GO

CREATE INDEX [IX_Orders_PromotionID] ON [Orders] ([PromotionID]);
GO

CREATE INDEX [IX_Payments_OrderID] ON [Payments] ([OrderID]);
GO

CREATE INDEX [IX_Products_CategoryID] ON [Products] ([CategoryID]);
GO

CREATE INDEX [IX_Users_RoleID] ON [Users] ([RoleID]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260206033455_MigrateToNewERD_Final', N'8.0.0');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

CREATE TABLE [Categories] (
    [CategoryID] int NOT NULL IDENTITY,
    [CategoryName] nvarchar(100) NOT NULL,
    CONSTRAINT [PK_Categories] PRIMARY KEY ([CategoryID])
);
GO

CREATE TABLE [Customers] (
    [CustomerID] int NOT NULL IDENTITY,
    [FullName] nvarchar(100) NOT NULL,
    [PhoneNumber] nvarchar(20) NULL,
    [Points] int NOT NULL,
    CONSTRAINT [PK_Customers] PRIMARY KEY ([CustomerID])
);
GO

CREATE TABLE [Promotions] (
    [PromotionID] int NOT NULL IDENTITY,
    [PromotionName] nvarchar(100) NOT NULL,
    [DiscountType] nvarchar(max) NOT NULL,
    [DiscountValue] decimal(18,2) NOT NULL,
    [StartDate] datetime2 NOT NULL,
    [EndDate] datetime2 NOT NULL,
    [IsActive] bit NOT NULL,
    CONSTRAINT [PK_Promotions] PRIMARY KEY ([PromotionID])
);
GO

CREATE TABLE [Roles] (
    [RoleID] int NOT NULL IDENTITY,
    [RoleName] nvarchar(50) NOT NULL,
    CONSTRAINT [PK_Roles] PRIMARY KEY ([RoleID])
);
GO

CREATE TABLE [Products] (
    [ProductID] int NOT NULL IDENTITY,
    [ProductName] nvarchar(100) NOT NULL,
    [CategoryID] int NOT NULL,
    [Price] decimal(18,2) NOT NULL,
    [StockQuantity] int NOT NULL,
    [ImageURL] nvarchar(255) NULL,
    [IsAvailable] bit NOT NULL,
    CONSTRAINT [PK_Products] PRIMARY KEY ([ProductID]),
    CONSTRAINT [FK_Products_Categories_CategoryID] FOREIGN KEY ([CategoryID]) REFERENCES [Categories] ([CategoryID]) ON DELETE CASCADE
);
GO

CREATE TABLE [Users] (
    [UserID] uniqueidentifier NOT NULL,
    [FullName] nvarchar(100) NOT NULL,
    [Username] nvarchar(50) NOT NULL,
    [Email] nvarchar(max) NULL,
    [Password] nvarchar(max) NULL,
    [RoleID] int NOT NULL,
    [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
    [CreatedAt] datetime2 NOT NULL DEFAULT (GETDATE()),
    CONSTRAINT [PK_Users] PRIMARY KEY ([UserID]),
    CONSTRAINT [FK_Users_Roles_RoleID] FOREIGN KEY ([RoleID]) REFERENCES [Roles] ([RoleID]) ON DELETE CASCADE
);
GO

CREATE TABLE [InventoryLogs] (
    [LogID] int NOT NULL IDENTITY,
    [ProductID] int NOT NULL,
    [QuantityChange] int NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_InventoryLogs] PRIMARY KEY ([LogID]),
    CONSTRAINT [FK_InventoryLogs_Products_ProductID] FOREIGN KEY ([ProductID]) REFERENCES [Products] ([ProductID]) ON DELETE CASCADE
);
GO

CREATE TABLE [AuditLogs] (
    [AuditID] int NOT NULL IDENTITY,
    [UserID] uniqueidentifier NULL,
    [Action] nvarchar(max) NOT NULL,
    [Timestamp] datetime2 NOT NULL,
    CONSTRAINT [PK_AuditLogs] PRIMARY KEY ([AuditID]),
    CONSTRAINT [FK_AuditLogs_Users_UserID] FOREIGN KEY ([UserID]) REFERENCES [Users] ([UserID])
);
GO

CREATE TABLE [Orders] (
    [OrderID] uniqueidentifier NOT NULL,
    [OrderDate] datetime2 NOT NULL DEFAULT (GETDATE()),
    [CashierID] uniqueidentifier NOT NULL,
    [CustomerID] int NULL,
    [PromotionID] int NULL,
    [TotalAmount] decimal(18,2) NOT NULL,
    [DiscountAmount] decimal(18,2) NOT NULL,
    [FinalAmount] decimal(18,2) NOT NULL,
    [PaymentStatus] nvarchar(20) NOT NULL,
    [PaymentMethod] nvarchar(50) NOT NULL,
    CONSTRAINT [PK_Orders] PRIMARY KEY ([OrderID]),
    CONSTRAINT [FK_Orders_Customers_CustomerID] FOREIGN KEY ([CustomerID]) REFERENCES [Customers] ([CustomerID]),
    CONSTRAINT [FK_Orders_Promotions_PromotionID] FOREIGN KEY ([PromotionID]) REFERENCES [Promotions] ([PromotionID]),
    CONSTRAINT [FK_Orders_Users_CashierID] FOREIGN KEY ([CashierID]) REFERENCES [Users] ([UserID]) ON DELETE NO ACTION
);
GO

CREATE TABLE [OrderDetails] (
    [OrderDetailID] int NOT NULL IDENTITY,
    [OrderID] uniqueidentifier NOT NULL,
    [ProductID] int NOT NULL,
    [Quantity] int NOT NULL,
    [UnitPrice] decimal(18,2) NOT NULL,
    [Subtotal] decimal(18,2) NOT NULL,
    CONSTRAINT [PK_OrderDetails] PRIMARY KEY ([OrderDetailID]),
    CONSTRAINT [FK_OrderDetails_Orders_OrderID] FOREIGN KEY ([OrderID]) REFERENCES [Orders] ([OrderID]) ON DELETE CASCADE,
    CONSTRAINT [FK_OrderDetails_Products_ProductID] FOREIGN KEY ([ProductID]) REFERENCES [Products] ([ProductID]) ON DELETE CASCADE
);
GO

CREATE TABLE [Payments] (
    [PaymentID] int NOT NULL IDENTITY,
    [OrderID] uniqueidentifier NOT NULL,
    [AmountPaid] decimal(18,2) NOT NULL,
    [PaymentMethod] nvarchar(50) NOT NULL,
    [ReferenceNumber] nvarchar(100) NULL,
    [PaymentDate] datetime2 NOT NULL,
    [PaymentStatus] nvarchar(20) NOT NULL,
    CONSTRAINT [PK_Payments] PRIMARY KEY ([PaymentID]),
    CONSTRAINT [FK_Payments_Orders_OrderID] FOREIGN KEY ([OrderID]) REFERENCES [Orders] ([OrderID]) ON DELETE CASCADE
);
GO

CREATE INDEX [IX_AuditLogs_UserID] ON [AuditLogs] ([UserID]);
GO

CREATE INDEX [IX_InventoryLogs_ProductID] ON [InventoryLogs] ([ProductID]);
GO

CREATE INDEX [IX_OrderDetails_OrderID] ON [OrderDetails] ([OrderID]);
GO

CREATE INDEX [IX_OrderDetails_ProductID] ON [OrderDetails] ([ProductID]);
GO

CREATE INDEX [IX_Orders_CashierID] ON [Orders] ([CashierID]);
GO

CREATE INDEX [IX_Orders_CustomerID] ON [Orders] ([CustomerID]);
GO

CREATE INDEX [IX_Orders_PromotionID] ON [Orders] ([PromotionID]);
GO

CREATE INDEX [IX_Payments_OrderID] ON [Payments] ([OrderID]);
GO

CREATE INDEX [IX_Products_CategoryID] ON [Products] ([CategoryID]);
GO

CREATE INDEX [IX_Users_RoleID] ON [Users] ([RoleID]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260209025222_InitialMonsterMig', N'8.0.0');
GO

COMMIT;
GO

