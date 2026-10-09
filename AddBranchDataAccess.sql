BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008020000_AddBranchDataAccess'
)
BEGIN
    ALTER TABLE [Inquiries] ADD [BranchId] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008020000_AddBranchDataAccess'
)
BEGIN
    ALTER TABLE [Feedbacks] ADD [BranchId] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008020000_AddBranchDataAccess'
)
BEGIN
    ALTER TABLE [Customers] ADD [BranchId] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008020000_AddBranchDataAccess'
)
BEGIN
    CREATE INDEX [IX_Inquiries_BranchId] ON [Inquiries] ([BranchId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008020000_AddBranchDataAccess'
)
BEGIN
    CREATE INDEX [IX_Feedbacks_BranchId] ON [Feedbacks] ([BranchId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008020000_AddBranchDataAccess'
)
BEGIN
    CREATE INDEX [IX_Customers_BranchId] ON [Customers] ([BranchId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008020000_AddBranchDataAccess'
)
BEGIN
    ALTER TABLE [Customers] ADD CONSTRAINT [FK_Customers_Branches_BranchId] FOREIGN KEY ([BranchId]) REFERENCES [Branches] ([BranchId]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008020000_AddBranchDataAccess'
)
BEGIN
    ALTER TABLE [Feedbacks] ADD CONSTRAINT [FK_Feedbacks_Branches_BranchId] FOREIGN KEY ([BranchId]) REFERENCES [Branches] ([BranchId]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008020000_AddBranchDataAccess'
)
BEGIN
    ALTER TABLE [Inquiries] ADD CONSTRAINT [FK_Inquiries_Branches_BranchId] FOREIGN KEY ([BranchId]) REFERENCES [Branches] ([BranchId]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008020000_AddBranchDataAccess'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261008020000_AddBranchDataAccess', N'10.0.11');
END;

COMMIT;
GO

