using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace freshcrumbs.CRM.infrastructure.Migrations.TenantCrmDb
{
    /// <inheritdoc />
    public partial class AddPromotionBranch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BranchId",
                table: "Promotions",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Promotions_BranchId",
                table: "Promotions",
                column: "BranchId");

            migrationBuilder.AddForeignKey(
                name: "FK_Promotions_Branches_BranchId",
                table: "Promotions",
                column: "BranchId",
                principalTable: "Branches",
                principalColumn: "BranchId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Promotions_Branches_BranchId",
                table: "Promotions");

            migrationBuilder.DropIndex(
                name: "IX_Promotions_BranchId",
                table: "Promotions");

            migrationBuilder.DropColumn(
                name: "BranchId",
                table: "Promotions");
        }
    }
}
