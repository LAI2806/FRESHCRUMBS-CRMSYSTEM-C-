using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace freshcrumbs.CRM.infrastructure.Migrations.TenantCrmDb
{
    /// <inheritdoc />
    public partial class AddBranching : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BranchId",
                table: "SalesTransactions",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Branches",
                columns: table => new
                {
                    BranchId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RowGuid = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    BranchName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Address = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Branches", x => x.BranchId);
                });

            migrationBuilder.CreateTable(
                name: "BranchAssignments",
                columns: table => new
                {
                    BranchAssignmentId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RowGuid = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    UserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BranchAssignments", x => x.BranchAssignmentId);
                    table.ForeignKey(
                        name: "FK_BranchAssignments_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "BranchId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BranchInventories",
                columns: table => new
                {
                    BranchInventoryId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RowGuid = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BranchInventories", x => x.BranchInventoryId);
                    table.ForeignKey(
                        name: "FK_BranchInventories_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "BranchId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BranchInventories_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "ProductId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SalesTransactions_BranchId",
                table: "SalesTransactions",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_BranchAssignments_BranchId",
                table: "BranchAssignments",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_BranchAssignments_RowGuid",
                table: "BranchAssignments",
                column: "RowGuid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BranchAssignments_UserId",
                table: "BranchAssignments",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Branches_RowGuid",
                table: "Branches",
                column: "RowGuid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BranchInventories_BranchId_ProductId",
                table: "BranchInventories",
                columns: new[] { "BranchId", "ProductId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BranchInventories_ProductId",
                table: "BranchInventories",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_BranchInventories_RowGuid",
                table: "BranchInventories",
                column: "RowGuid",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_SalesTransactions_Branches_BranchId",
                table: "SalesTransactions",
                column: "BranchId",
                principalTable: "Branches",
                principalColumn: "BranchId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SalesTransactions_Branches_BranchId",
                table: "SalesTransactions");

            migrationBuilder.DropTable(
                name: "BranchAssignments");

            migrationBuilder.DropTable(
                name: "BranchInventories");

            migrationBuilder.DropTable(
                name: "Branches");

            migrationBuilder.DropIndex(
                name: "IX_SalesTransactions_BranchId",
                table: "SalesTransactions");

            migrationBuilder.DropColumn(
                name: "BranchId",
                table: "SalesTransactions");
        }
    }
}
