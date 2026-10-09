using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace freshcrumbs.CRM.infrastructure.Migrations.TenantCrmDb
{
    /// <inheritdoc />
    public partial class AddBranchDataAccess : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BranchId",
                table: "Inquiries",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BranchId",
                table: "Feedbacks",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BranchId",
                table: "Customers",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Inquiries_BranchId",
                table: "Inquiries",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_Feedbacks_BranchId",
                table: "Feedbacks",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_BranchId",
                table: "Customers",
                column: "BranchId");

            migrationBuilder.AddForeignKey(
                name: "FK_Customers_Branches_BranchId",
                table: "Customers",
                column: "BranchId",
                principalTable: "Branches",
                principalColumn: "BranchId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Feedbacks_Branches_BranchId",
                table: "Feedbacks",
                column: "BranchId",
                principalTable: "Branches",
                principalColumn: "BranchId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Inquiries_Branches_BranchId",
                table: "Inquiries",
                column: "BranchId",
                principalTable: "Branches",
                principalColumn: "BranchId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Customers_Branches_BranchId",
                table: "Customers");

            migrationBuilder.DropForeignKey(
                name: "FK_Feedbacks_Branches_BranchId",
                table: "Feedbacks");

            migrationBuilder.DropForeignKey(
                name: "FK_Inquiries_Branches_BranchId",
                table: "Inquiries");

            migrationBuilder.DropIndex(
                name: "IX_Inquiries_BranchId",
                table: "Inquiries");

            migrationBuilder.DropIndex(
                name: "IX_Feedbacks_BranchId",
                table: "Feedbacks");

            migrationBuilder.DropIndex(
                name: "IX_Customers_BranchId",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "BranchId",
                table: "Inquiries");

            migrationBuilder.DropColumn(
                name: "BranchId",
                table: "Feedbacks");

            migrationBuilder.DropColumn(
                name: "BranchId",
                table: "Customers");
        }
    }
}
