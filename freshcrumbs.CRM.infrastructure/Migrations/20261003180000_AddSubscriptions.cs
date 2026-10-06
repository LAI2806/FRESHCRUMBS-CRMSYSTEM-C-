using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using freshcrumbs.CRM.infrastructure.data;

#nullable disable

namespace freshcrumbs.CRM.infrastructure.Migrations
{
    [DbContext(typeof(MasterCrmDbContext))]
    [Migration("20261003180000_AddSubscriptions")]
    public partial class AddSubscriptions : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Subscriptions",
                columns: table => new
                {
                    SubscriptionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    PlanId = table.Column<int>(type: "int", nullable: false),
                    PlanCode = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    PlanName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    BillingCycle = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Features = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    MaxUsers = table.Column<int>(type: "int", nullable: false),
                    BranchingEnabled = table.Column<bool>(type: "bit", nullable: false),
                    MaxBranches = table.Column<int>(type: "int", nullable: true),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ChangeType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ChangedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    PreviousSubscriptionId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Subscriptions", x => x.SubscriptionId);
                    table.CheckConstraint("CK_Subscriptions_Price", "[Price] >= 0");
                    table.CheckConstraint("CK_Subscriptions_MaxUsers", "[MaxUsers] >= 1");
                    table.CheckConstraint("CK_Subscriptions_Dates", "[EndDate] >= [StartDate]");
                    table.CheckConstraint("CK_Subscriptions_Branching", "([BranchingEnabled] = 1 AND [MaxBranches] >= 1) OR ([BranchingEnabled] = 0 AND [MaxBranches] IS NULL)");
                    table.ForeignKey(
                        name: "FK_Subscriptions_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "CompanyId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Subscriptions_Plans_PlanId",
                        column: x => x.PlanId,
                        principalTable: "Plans",
                        principalColumn: "PlanId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Subscriptions_Subscriptions_PreviousSubscriptionId",
                        column: x => x.PreviousSubscriptionId,
                        principalTable: "Subscriptions",
                        principalColumn: "SubscriptionId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_CompanyId_StartDate",
                table: "Subscriptions",
                columns: new[] { "CompanyId", "StartDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_PlanId",
                table: "Subscriptions",
                column: "PlanId");

            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_PreviousSubscriptionId",
                table: "Subscriptions",
                column: "PreviousSubscriptionId");

            // ---- Backfill for companies that existed before subscriptions ----
            // 1) A clearly named, INACTIVE transition plan (cannot be assigned to new subscribers).
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM [Plans] WHERE [PlanCode] = 'LEGACY-TRANSITION')
BEGIN
    INSERT INTO [Plans] ([PlanCode], [DisplayName], [Description], [Price], [BillingCycle], [MaxUsers], [BranchingEnabled], [MaxBranches], [IsActive], [CreatedAt], [UpdatedAt])
    VALUES ('LEGACY-TRANSITION', 'Legacy Transition (Backfill)',
            'System-created for companies that existed before subscriptions. Not assignable to new subscribers. Replace with a real plan.',
            0, 'Monthly', 1000, 0, NULL, 0, SYSUTCDATETIME(), NULL);

    INSERT INTO [PlanFeatures] ([PlanId], [FeatureKey])
    SELECT p.[PlanId], f.[FeatureKey]
    FROM [Plans] p
    CROSS JOIN (VALUES ('MainTransactions'), ('DataCollection'), ('BusinessIntelligence'), ('ActionsRetention')) AS f([FeatureKey])
    WHERE p.[PlanCode] = 'LEGACY-TRANSITION';
END");

            // 2) A time-limited (90 day) subscription for every company that has none.
            migrationBuilder.Sql(@"
INSERT INTO [Subscriptions]
    ([CompanyId], [PlanId], [PlanCode], [PlanName], [Price], [BillingCycle], [Features], [MaxUsers], [BranchingEnabled], [MaxBranches],
     [StartDate], [EndDate], [Status], [ChangeType], [Reason], [ChangedBy], [PreviousSubscriptionId], [CreatedAt])
SELECT c.[CompanyId], p.[PlanId], p.[PlanCode], p.[DisplayName], p.[Price], p.[BillingCycle],
       'MainTransactions,DataCollection,BusinessIntelligence,ActionsRetention', p.[MaxUsers], p.[BranchingEnabled], p.[MaxBranches],
       SYSUTCDATETIME(), DATEADD(DAY, 90, SYSUTCDATETIME()), 'Active', 'New',
       'Backfill: company existed before subscriptions were introduced. Assign a real plan before this expires.',
       'SYSTEM', NULL, SYSUTCDATETIME()
FROM [Companies] c
CROSS JOIN [Plans] p
WHERE p.[PlanCode] = 'LEGACY-TRANSITION'
  AND NOT EXISTS (SELECT 1 FROM [Subscriptions] s WHERE s.[CompanyId] = c.[CompanyId]);");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "Subscriptions");

            migrationBuilder.Sql(@"
DELETE FROM [PlanFeatures] WHERE [PlanId] IN (SELECT [PlanId] FROM [Plans] WHERE [PlanCode] = 'LEGACY-TRANSITION');
DELETE FROM [Plans] WHERE [PlanCode] = 'LEGACY-TRANSITION';");
        }
    }
}