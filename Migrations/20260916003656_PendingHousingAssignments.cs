using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HousingManagementWeb.Migrations
{
    /// <inheritdoc />
    public partial class PendingHousingAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PendingHousingAssignments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EmployeeId = table.Column<int>(type: "INTEGER", nullable: false),
                    JobTitle = table.Column<string>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<int>(type: "INTEGER", nullable: false),
                    SiteId = table.Column<int>(type: "INTEGER", nullable: false),
                    RoomId = table.Column<int>(type: "INTEGER", nullable: false),
                    HousingDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsApplied = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PendingHousingAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PendingHousingAssignments_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PendingHousingAssignments_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PendingHousingAssignments_Rooms_RoomId",
                        column: x => x.RoomId,
                        principalTable: "Rooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PendingHousingAssignments_Sites_SiteId",
                        column: x => x.SiteId,
                        principalTable: "Sites",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PendingHousingAssignments_EmployeeId",
                table: "PendingHousingAssignments",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_PendingHousingAssignments_ProjectId",
                table: "PendingHousingAssignments",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_PendingHousingAssignments_RoomId",
                table: "PendingHousingAssignments",
                column: "RoomId");

            migrationBuilder.CreateIndex(
                name: "IX_PendingHousingAssignments_SiteId",
                table: "PendingHousingAssignments",
                column: "SiteId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PendingHousingAssignments");
        }
    }
}
