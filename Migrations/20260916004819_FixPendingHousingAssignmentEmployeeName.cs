using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HousingManagementWeb.Migrations
{
    /// <inheritdoc />
    public partial class FixPendingHousingAssignmentEmployeeName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EmployeeName",
                table: "PendingHousingAssignments",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EmployeeName",
                table: "PendingHousingAssignments");
        }
    }
}
