using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace freshcrumbs.CRM.infrastructure.Migrations.TenantCrmDb
{
    /// <inheritdoc />
    public partial class CompleteMultipleDiscountEligibility : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Legacy column: exists only on older databases; fresh databases never had it.
            migrationBuilder.Sql(@"
                IF COL_LENGTH(N'dbo.Customers', N'DiscountEligibility') IS NOT NULL
                BEGIN
                    DECLARE @constraintName nvarchar(128);
                    SELECT @constraintName = dc.name
                    FROM sys.default_constraints dc
                    INNER JOIN sys.columns col
                        ON col.object_id = dc.parent_object_id
                        AND col.column_id = dc.parent_column_id
                    WHERE dc.parent_object_id = OBJECT_ID(N'dbo.Customers')
                        AND col.name = N'DiscountEligibility';

                    IF @constraintName IS NOT NULL
                        EXEC(N'ALTER TABLE [dbo].[Customers] DROP CONSTRAINT [' + @constraintName + N']');

                    EXEC(N'ALTER TABLE [dbo].[Customers] DROP COLUMN [DiscountEligibility]');
                END
            ");

            // Legacy column: exists only on older databases; fresh databases never had it.
            migrationBuilder.Sql(@"
                IF COL_LENGTH(N'dbo.Customers', N'DiscountIdNumber') IS NOT NULL
                BEGIN
                    DECLARE @constraintName nvarchar(128);
                    SELECT @constraintName = dc.name
                    FROM sys.default_constraints dc
                    INNER JOIN sys.columns col
                        ON col.object_id = dc.parent_object_id
                        AND col.column_id = dc.parent_column_id
                    WHERE dc.parent_object_id = OBJECT_ID(N'dbo.Customers')
                        AND col.name = N'DiscountIdNumber';

                    IF @constraintName IS NOT NULL
                        EXEC(N'ALTER TABLE [dbo].[Customers] DROP CONSTRAINT [' + @constraintName + N']');

                    EXEC(N'ALTER TABLE [dbo].[Customers] DROP COLUMN [DiscountIdNumber]');
                END
            ");

            // Legacy column: exists only on older databases; fresh databases never had it.
            migrationBuilder.Sql(@"
                IF COL_LENGTH(N'dbo.Customers', N'VerificationStatus') IS NOT NULL
                BEGIN
                    DECLARE @constraintName nvarchar(128);
                    SELECT @constraintName = dc.name
                    FROM sys.default_constraints dc
                    INNER JOIN sys.columns col
                        ON col.object_id = dc.parent_object_id
                        AND col.column_id = dc.parent_column_id
                    WHERE dc.parent_object_id = OBJECT_ID(N'dbo.Customers')
                        AND col.name = N'VerificationStatus';

                    IF @constraintName IS NOT NULL
                        EXEC(N'ALTER TABLE [dbo].[Customers] DROP CONSTRAINT [' + @constraintName + N']');

                    EXEC(N'ALTER TABLE [dbo].[Customers] DROP COLUMN [VerificationStatus]');
                END
            ");

            migrationBuilder.AddColumn<string>(
                name: "EligibilityCategory",
                table: "Promotions",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CustomerDiscountEligibilities",
                columns: table => new
                {
                    EligibilityId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerId = table.Column<int>(type: "int", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IdNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    VerificationStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerDiscountEligibilities", x => x.EligibilityId);
                    table.ForeignKey(
                        name: "FK_CustomerDiscountEligibilities_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "CustomerId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerDiscountEligibilities_CustomerId_Category",
                table: "CustomerDiscountEligibilities",
                columns: new[] { "CustomerId", "Category" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CustomerDiscountEligibilities");

            migrationBuilder.DropColumn(
                name: "EligibilityCategory",
                table: "Promotions");

            migrationBuilder.AddColumn<string>(
                name: "DiscountEligibility",
                table: "Customers",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "DiscountIdNumber",
                table: "Customers",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "VerificationStatus",
                table: "Customers",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");
        }
    }
}