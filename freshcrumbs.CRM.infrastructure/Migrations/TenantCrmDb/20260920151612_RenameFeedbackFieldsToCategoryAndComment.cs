using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace freshcrumbs.CRM.infrastructure.Migrations.TenantCrmDb
{
    /// <inheritdoc />
    public partial class RenameFeedbackFieldsToCategoryAndComment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Subject",
                table: "Feedbacks",
                newName: "Category");

            migrationBuilder.RenameColumn(
                name: "Description",
                table: "Feedbacks",
                newName: "Comment");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Comment",
                table: "Feedbacks",
                newName: "Description");

            migrationBuilder.RenameColumn(
                name: "Category",
                table: "Feedbacks",
                newName: "Subject");
        }
    }
}
