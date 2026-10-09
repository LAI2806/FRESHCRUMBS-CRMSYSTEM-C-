BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008034030_AddPromotionBranch'
)
BEGIN
    ALTER TABLE [Promotions] ADD [BranchId] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008034030_AddPromotionBranch'
)
BEGIN
    CREATE INDEX [IX_Promotions_BranchId] ON [Promotions] ([BranchId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008034030_AddPromotionBranch'
)
BEGIN
    ALTER TABLE [Promotions] ADD CONSTRAINT [FK_Promotions_Branches_BranchId] FOREIGN KEY ([BranchId]) REFERENCES [Branches] ([BranchId]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008034030_AddPromotionBranch'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261008034030_AddPromotionBranch', N'10.0.11');
END;

COMMIT;
GO

