BEGIN TRANSACTION;
DECLARE @var nvarchar(max);
SELECT @var = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Customers]') AND [c].[name] = N'DiscountEligibility');
IF @var IS NOT NULL EXEC(N'ALTER TABLE [Customers] DROP CONSTRAINT ' + @var + ';');
ALTER TABLE [Customers] DROP COLUMN [DiscountEligibility];

DECLARE @var1 nvarchar(max);
SELECT @var1 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Customers]') AND [c].[name] = N'DiscountIdNumber');
IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [Customers] DROP CONSTRAINT ' + @var1 + ';');
ALTER TABLE [Customers] DROP COLUMN [DiscountIdNumber];

DECLARE @var2 nvarchar(max);
SELECT @var2 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Customers]') AND [c].[name] = N'VerificationStatus');
IF @var2 IS NOT NULL EXEC(N'ALTER TABLE [Customers] DROP CONSTRAINT ' + @var2 + ';');
ALTER TABLE [Customers] DROP COLUMN [VerificationStatus];

ALTER TABLE [Promotions] ADD [EligibilityCategory] nvarchar(50) NULL;

CREATE TABLE [CustomerDiscountEligibilities] (
    [EligibilityId] int NOT NULL IDENTITY,
    [CustomerId] int NOT NULL,
    [Category] nvarchar(50) NOT NULL,
    [IdNumber] nvarchar(50) NOT NULL,
    [VerificationStatus] nvarchar(30) NOT NULL,
    CONSTRAINT [PK_CustomerDiscountEligibilities] PRIMARY KEY ([EligibilityId]),
    CONSTRAINT [FK_CustomerDiscountEligibilities_Customers_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [Customers] ([CustomerId]) ON DELETE CASCADE
);

CREATE UNIQUE INDEX [IX_CustomerDiscountEligibilities_CustomerId_Category] ON [CustomerDiscountEligibilities] ([CustomerId], [Category]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260930154813_CompleteMultipleDiscountEligibility', N'10.0.11');

COMMIT;
GO

