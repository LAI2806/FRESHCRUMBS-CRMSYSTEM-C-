using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using freshcrumbs.CRM.infrastructure.data;

#nullable disable

namespace freshcrumbs.CRM.infrastructure.Migrations
{
    [DbContext(typeof(MasterCrmDbContext))]
    [Migration("20261003120000_AddSuperAdminAndPlans")]
    public partial class AddSuperAdminAndPlans : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUsers_Companies_TenantId",
                table: "AspNetUsers");

            migrationBuilder.AlterColumn<int>(
                name: "TenantId",
                table: "AspNetUsers",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_Companies_TenantId",
                table: "AspNetUsers",
                column: "TenantId",
                principalTable: "Companies",
                principalColumn: "CompanyId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.CreateTable(
                name: "Plans",
                columns: table => new
                {
                    PlanId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PlanCode = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    BillingCycle = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    MaxUsers = table.Column<int>(type: "int", nullable: false),
                    BranchingEnabled = table.Column<bool>(type: "bit", nullable: false),
                    MaxBranches = table.Column<int>(type: "int", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Plans", x => x.PlanId);
                    table.CheckConstraint("CK_Plans_Price", "[Price] >= 0");
                    table.CheckConstraint("CK_Plans_MaxUsers", "[MaxUsers] >= 1");
                    table.CheckConstraint("CK_Plans_Branching", "([BranchingEnabled] = 1 AND [MaxBranches] >= 1) OR ([BranchingEnabled] = 0 AND [MaxBranches] IS NULL)");
                });

            migrationBuilder.CreateTable(
                name: "PlanFeatures",
                columns: table => new
                {
                    PlanId = table.Column<int>(type: "int", nullable: false),
                    FeatureKey = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlanFeatures", x => new { x.PlanId, x.FeatureKey });
                    table.ForeignKey(
                        name: "FK_PlanFeatures_Plans_PlanId",
                        column: x => x.PlanId,
                        principalTable: "Plans",
                        principalColumn: "PlanId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Plans_PlanCode",
                table: "Plans",
                column: "PlanCode",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "PlanFeatures");

            migrationBuilder.DropTable(name: "Plans");

            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUsers_Companies_TenantId",
                table: "AspNetUsers");

            migrationBuilder.AlterColumn<int>(
                name: "TenantId",
                table: "AspNetUsers",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_Companies_TenantId",
                table: "AspNetUsers",
                column: "TenantId",
                principalTable: "Companies",
                principalColumn: "CompanyId",
                onDelete: ReferentialAction.Restrict);
        }
    }
}