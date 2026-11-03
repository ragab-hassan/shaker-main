using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HousingManagementWeb.Migrations
{
    /// <inheritdoc />
    public partial class LinkPendingAssignmentEmployees : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("PRAGMA foreign_keys = OFF;", suppressTransaction: true);
            migrationBuilder.Sql("""
                CREATE TABLE "ef_temp_PendingHousingAssignments" (
                    "Id" INTEGER NOT NULL CONSTRAINT "PK_PendingHousingAssignments" PRIMARY KEY AUTOINCREMENT,
                    "EmployeeId" INTEGER NULL,
                    "EmployeeName" TEXT NOT NULL,
                    "JobTitle" TEXT NOT NULL,
                    "ProjectId" INTEGER NOT NULL,
                    "SiteId" INTEGER NOT NULL,
                    "RoomId" INTEGER NOT NULL,
                    "HousingDate" TEXT NOT NULL,
                    "IsApplied" INTEGER NOT NULL,
                    CONSTRAINT "FK_PendingHousingAssignments_Employees_EmployeeId"
                        FOREIGN KEY ("EmployeeId") REFERENCES "Employees" ("Id") ON DELETE CASCADE,
                    CONSTRAINT "FK_PendingHousingAssignments_Projects_ProjectId"
                        FOREIGN KEY ("ProjectId") REFERENCES "Projects" ("Id") ON DELETE RESTRICT,
                    CONSTRAINT "FK_PendingHousingAssignments_Rooms_RoomId"
                        FOREIGN KEY ("RoomId") REFERENCES "Rooms" ("Id") ON DELETE RESTRICT,
                    CONSTRAINT "FK_PendingHousingAssignments_Sites_SiteId"
                        FOREIGN KEY ("SiteId") REFERENCES "Sites" ("Id") ON DELETE RESTRICT
                );

                INSERT INTO "ef_temp_PendingHousingAssignments"
                    ("Id", "EmployeeId", "EmployeeName", "JobTitle", "ProjectId", "SiteId", "RoomId", "HousingDate", "IsApplied")
                SELECT "Id", CASE WHEN "EmployeeId" = 0 THEN NULL ELSE "EmployeeId" END,
                    "EmployeeName", "JobTitle", "ProjectId", "SiteId", "RoomId", "HousingDate", "IsApplied"
                FROM "PendingHousingAssignments";

                DROP TABLE "PendingHousingAssignments";
                ALTER TABLE "ef_temp_PendingHousingAssignments" RENAME TO "PendingHousingAssignments";

                CREATE INDEX "IX_PendingHousingAssignments_EmployeeId" ON "PendingHousingAssignments" ("EmployeeId");
                CREATE INDEX "IX_PendingHousingAssignments_ProjectId" ON "PendingHousingAssignments" ("ProjectId");
                CREATE INDEX "IX_PendingHousingAssignments_RoomId" ON "PendingHousingAssignments" ("RoomId");
                CREATE INDEX "IX_PendingHousingAssignments_SiteId" ON "PendingHousingAssignments" ("SiteId");
                """);
            migrationBuilder.Sql("PRAGMA foreign_keys = ON;", suppressTransaction: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                INSERT INTO Employees (
                    EmployeeCode, FullName, NationalId, Address, Governorate, Phone,
                    ProjectName, ProjectId, JobTitle, BuildingNo, ApartmentNo, RoomNo,
                    RoomId, HousingDate, ResignationDate, IsActive)
                SELECT
                    'LEGACY-PENDING-' || Id,
                    COALESCE(NULLIF(EmployeeName, ''), 'Legacy employee ' || Id),
                    '', '', '', '', '', ProjectId, JobTitle, NULL, NULL, NULL,
                    NULL, HousingDate, NULL, 1
                FROM PendingHousingAssignments
                WHERE EmployeeId IS NULL;

                UPDATE PendingHousingAssignments
                SET EmployeeId = (
                    SELECT Id FROM Employees
                    WHERE EmployeeCode = 'LEGACY-PENDING-' || PendingHousingAssignments.Id
                )
                WHERE EmployeeId IS NULL;
                """);

            migrationBuilder.Sql("PRAGMA foreign_keys = OFF;", suppressTransaction: true);
            migrationBuilder.Sql("""
                CREATE TABLE "ef_temp_PendingHousingAssignments" (
                    "Id" INTEGER NOT NULL CONSTRAINT "PK_PendingHousingAssignments" PRIMARY KEY AUTOINCREMENT,
                    "EmployeeId" INTEGER NOT NULL,
                    "EmployeeName" TEXT NOT NULL,
                    "JobTitle" TEXT NOT NULL,
                    "ProjectId" INTEGER NOT NULL,
                    "SiteId" INTEGER NOT NULL,
                    "RoomId" INTEGER NOT NULL,
                    "HousingDate" TEXT NOT NULL,
                    "IsApplied" INTEGER NOT NULL,
                    CONSTRAINT "FK_PendingHousingAssignments_Employees_EmployeeId"
                        FOREIGN KEY ("EmployeeId") REFERENCES "Employees" ("Id") ON DELETE CASCADE,
                    CONSTRAINT "FK_PendingHousingAssignments_Projects_ProjectId"
                        FOREIGN KEY ("ProjectId") REFERENCES "Projects" ("Id") ON DELETE RESTRICT,
                    CONSTRAINT "FK_PendingHousingAssignments_Rooms_RoomId"
                        FOREIGN KEY ("RoomId") REFERENCES "Rooms" ("Id") ON DELETE RESTRICT,
                    CONSTRAINT "FK_PendingHousingAssignments_Sites_SiteId"
                        FOREIGN KEY ("SiteId") REFERENCES "Sites" ("Id") ON DELETE RESTRICT
                );

                INSERT INTO "ef_temp_PendingHousingAssignments"
                    ("Id", "EmployeeId", "EmployeeName", "JobTitle", "ProjectId", "SiteId", "RoomId", "HousingDate", "IsApplied")
                SELECT "Id", "EmployeeId", "EmployeeName", "JobTitle", "ProjectId", "SiteId", "RoomId", "HousingDate", "IsApplied"
                FROM "PendingHousingAssignments";

                DROP TABLE "PendingHousingAssignments";
                ALTER TABLE "ef_temp_PendingHousingAssignments" RENAME TO "PendingHousingAssignments";

                CREATE INDEX "IX_PendingHousingAssignments_EmployeeId" ON "PendingHousingAssignments" ("EmployeeId");
                CREATE INDEX "IX_PendingHousingAssignments_ProjectId" ON "PendingHousingAssignments" ("ProjectId");
                CREATE INDEX "IX_PendingHousingAssignments_RoomId" ON "PendingHousingAssignments" ("RoomId");
                CREATE INDEX "IX_PendingHousingAssignments_SiteId" ON "PendingHousingAssignments" ("SiteId");
                """);
            migrationBuilder.Sql("PRAGMA foreign_keys = ON;", suppressTransaction: true);
        }
    }
}
