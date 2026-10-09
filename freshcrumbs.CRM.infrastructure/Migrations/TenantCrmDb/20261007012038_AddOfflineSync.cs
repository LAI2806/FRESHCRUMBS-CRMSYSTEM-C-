using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace freshcrumbs.CRM.infrastructure.Migrations.TenantCrmDb
{
    /// <inheritdoc />
    public partial class AddOfflineSync : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "RowGuid",
                table: "TransactionItems",
                type: "uniqueidentifier",
                nullable: false,
                defaultValueSql: "NEWID()");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "TransactionItems",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "GETUTCDATE()");

            migrationBuilder.AddColumn<Guid>(
                name: "RowGuid",
                table: "SalesTransactions",
                type: "uniqueidentifier",
                nullable: false,
                defaultValueSql: "NEWID()");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "SalesTransactions",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "GETUTCDATE()");

            migrationBuilder.AddColumn<Guid>(
                name: "RowGuid",
                table: "Promotions",
                type: "uniqueidentifier",
                nullable: false,
                defaultValueSql: "NEWID()");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Promotions",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "GETUTCDATE()");

            migrationBuilder.AddColumn<Guid>(
                name: "RowGuid",
                table: "Products",
                type: "uniqueidentifier",
                nullable: false,
                defaultValueSql: "NEWID()");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Products",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "GETUTCDATE()");

            migrationBuilder.AddColumn<Guid>(
                name: "RowGuid",
                table: "LoyaltyTransactions",
                type: "uniqueidentifier",
                nullable: false,
                defaultValueSql: "NEWID()");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "LoyaltyTransactions",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "GETUTCDATE()");

            migrationBuilder.AddColumn<Guid>(
                name: "RowGuid",
                table: "Inquiries",
                type: "uniqueidentifier",
                nullable: false,
                defaultValueSql: "NEWID()");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Inquiries",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "GETUTCDATE()");

            migrationBuilder.AddColumn<Guid>(
                name: "RowGuid",
                table: "Feedbacks",
                type: "uniqueidentifier",
                nullable: false,
                defaultValueSql: "NEWID()");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Feedbacks",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "GETUTCDATE()");

            migrationBuilder.AddColumn<Guid>(
                name: "RowGuid",
                table: "Customers",
                type: "uniqueidentifier",
                nullable: false,
                defaultValueSql: "NEWID()");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Customers",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "GETUTCDATE()");

            migrationBuilder.AddColumn<Guid>(
                name: "RowGuid",
                table: "CustomerDiscountEligibilities",
                type: "uniqueidentifier",
                nullable: false,
                defaultValueSql: "NEWID()");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "CustomerDiscountEligibilities",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "GETUTCDATE()");

            migrationBuilder.CreateTable(
                name: "SyncOutbox",
                columns: table => new
                {
                    Sequence = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EntityRowGuid = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Operation = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Payload = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ChangedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    LastError = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    SyncedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SyncOutbox", x => x.Sequence);
                });

            migrationBuilder.CreateTable(
                name: "SyncReceipts",
                columns: table => new
                {
                    OperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EntityRowGuid = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Result = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Detail = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ProcessedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SyncReceipts", x => x.OperationId);
                });

            migrationBuilder.CreateTable(
                name: "SyncStates",
                columns: table => new
                {
                    Key = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SyncStates", x => x.Key);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TransactionItems_RowGuid",
                table: "TransactionItems",
                column: "RowGuid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalesTransactions_RowGuid",
                table: "SalesTransactions",
                column: "RowGuid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Promotions_RowGuid",
                table: "Promotions",
                column: "RowGuid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Products_RowGuid",
                table: "Products",
                column: "RowGuid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyTransactions_RowGuid",
                table: "LoyaltyTransactions",
                column: "RowGuid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Inquiries_RowGuid",
                table: "Inquiries",
                column: "RowGuid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Feedbacks_RowGuid",
                table: "Feedbacks",
                column: "RowGuid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Customers_RowGuid",
                table: "Customers",
                column: "RowGuid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerDiscountEligibilities_RowGuid",
                table: "CustomerDiscountEligibilities",
                column: "RowGuid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SyncOutbox_EntityRowGuid",
                table: "SyncOutbox",
                column: "EntityRowGuid");

            migrationBuilder.CreateIndex(
                name: "IX_SyncOutbox_OperationId",
                table: "SyncOutbox",
                column: "OperationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SyncOutbox_Status_Sequence",
                table: "SyncOutbox",
                columns: new[] { "Status", "Sequence" });

            migrationBuilder.CreateIndex(
                name: "IX_SyncReceipts_ProcessedAtUtc",
                table: "SyncReceipts",
                column: "ProcessedAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SyncOutbox");

            migrationBuilder.DropTable(
                name: "SyncReceipts");

            migrationBuilder.DropTable(
                name: "SyncStates");

            migrationBuilder.DropIndex(
                name: "IX_TransactionItems_RowGuid",
                table: "TransactionItems");

            migrationBuilder.DropIndex(
                name: "IX_SalesTransactions_RowGuid",
                table: "SalesTransactions");

            migrationBuilder.DropIndex(
                name: "IX_Promotions_RowGuid",
                table: "Promotions");

            migrationBuilder.DropIndex(
                name: "IX_Products_RowGuid",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_LoyaltyTransactions_RowGuid",
                table: "LoyaltyTransactions");

            migrationBuilder.DropIndex(
                name: "IX_Inquiries_RowGuid",
                table: "Inquiries");

            migrationBuilder.DropIndex(
                name: "IX_Feedbacks_RowGuid",
                table: "Feedbacks");

            migrationBuilder.DropIndex(
                name: "IX_Customers_RowGuid",
                table: "Customers");

            migrationBuilder.DropIndex(
                name: "IX_CustomerDiscountEligibilities_RowGuid",
                table: "CustomerDiscountEligibilities");

            migrationBuilder.DropColumn(
                name: "RowGuid",
                table: "TransactionItems");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "TransactionItems");

            migrationBuilder.DropColumn(
                name: "RowGuid",
                table: "SalesTransactions");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "SalesTransactions");

            migrationBuilder.DropColumn(
                name: "RowGuid",
                table: "Promotions");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Promotions");

            migrationBuilder.DropColumn(
                name: "RowGuid",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "RowGuid",
                table: "LoyaltyTransactions");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "LoyaltyTransactions");

            migrationBuilder.DropColumn(
                name: "RowGuid",
                table: "Inquiries");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Inquiries");

            migrationBuilder.DropColumn(
                name: "RowGuid",
                table: "Feedbacks");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Feedbacks");

            migrationBuilder.DropColumn(
                name: "RowGuid",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "RowGuid",
                table: "CustomerDiscountEligibilities");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "CustomerDiscountEligibilities");
        }
    }
}
