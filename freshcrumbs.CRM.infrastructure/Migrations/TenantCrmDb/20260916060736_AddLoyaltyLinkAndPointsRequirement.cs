using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace freshcrumbs.CRM.infrastructure.Migrations.TenantCrmDb
{
    /// <inheritdoc />
    public partial class AddLoyaltyLinkAndPointsRequirement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RequiredLoyaltyPoints",
                table: "Promotions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SalesTransactionId",
                table: "LoyaltyTransactions",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyTransactions_SalesTransactionId",
                table: "LoyaltyTransactions",
                column: "SalesTransactionId");

            migrationBuilder.AddForeignKey(
                name: "FK_LoyaltyTransactions_SalesTransactions_SalesTransactionId",
                table: "LoyaltyTransactions",
                column: "SalesTransactionId",
                principalTable: "SalesTransactions",
                principalColumn: "TransactionId",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LoyaltyTransactions_SalesTransactions_SalesTransactionId",
                table: "LoyaltyTransactions");

            migrationBuilder.DropIndex(
                name: "IX_LoyaltyTransactions_SalesTransactionId",
                table: "LoyaltyTransactions");

            migrationBuilder.DropColumn(
                name: "RequiredLoyaltyPoints",
                table: "Promotions");

            migrationBuilder.DropColumn(
                name: "SalesTransactionId",
                table: "LoyaltyTransactions");
        }
    }
}
