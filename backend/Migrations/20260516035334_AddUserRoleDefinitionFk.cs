using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tracker.Migrations
{
    /// <inheritdoc />
    public partial class AddUserRoleDefinitionFk : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "RoleDefinitionId",
                table: "Users",
                type: "uniqueidentifier",
                nullable: true);

            // Seed the 4 default RoleDefinitions for every existing tenant that doesn't
            // already have a role with the same name. SortOrder maps to Number — we
            // re-sequence inside each tenant so the Number unique index isn't violated.
            migrationBuilder.Sql(@"
DECLARE @now datetime2 = SYSUTCDATETIME();

;WITH defaults(name, descr, canView, canAdd, canEdit, canDelete, canReports) AS (
    SELECT 'Admin',    'Full access to everything in the workspace.', 1, 1, 1, 1, 1 UNION ALL
    SELECT 'Manager',  'View, add, edit and view reports — cannot delete.', 1, 1, 1, 0, 1 UNION ALL
    SELECT 'Operator', 'Day-to-day floor operator: view, add and edit only.', 1, 1, 1, 0, 0 UNION ALL
    SELECT 'Viewer',   'Read-only access — including reports.', 1, 0, 0, 0, 1
)
INSERT INTO RoleDefinitions
    (Id, TenantId, Number, Name, Description, CanView, CanAdd, CanEdit, CanDelete, CanViewReports, IsActive, CreatedAtUtc)
SELECT
    NEWID(),
    t.Id,
    -- continue numbering after any roles that already exist for this tenant
    ISNULL((SELECT MAX(r2.Number) FROM RoleDefinitions r2 WHERE r2.TenantId = t.Id), 0)
        + ROW_NUMBER() OVER (PARTITION BY t.Id ORDER BY
            CASE d.name WHEN 'Admin' THEN 1 WHEN 'Manager' THEN 2 WHEN 'Operator' THEN 3 ELSE 4 END),
    d.name, d.descr, d.canView, d.canAdd, d.canEdit, d.canDelete, d.canReports, 1, @now
FROM Tenants t
CROSS JOIN defaults d
WHERE NOT EXISTS (
    SELECT 1 FROM RoleDefinitions r
    WHERE r.TenantId = t.Id AND r.Name = d.name
);
");

            // Backfill Users.RoleDefinitionId: match by name (case-insensitive) within the same
            // tenant. Users whose Role string doesn't match any role stay NULL — the
            // PermissionService falls back to view-only for them, which is a safe default.
            migrationBuilder.Sql(@"
UPDATE u
SET u.RoleDefinitionId = r.Id
FROM Users u
INNER JOIN RoleDefinitions r
    ON r.TenantId = u.TenantId
   AND LOWER(r.Name) = LOWER(u.Role);
");

            migrationBuilder.CreateIndex(
                name: "IX_Users_RoleDefinitionId",
                table: "Users",
                column: "RoleDefinitionId");

            migrationBuilder.AddForeignKey(
                name: "FK_Users_RoleDefinitions_RoleDefinitionId",
                table: "Users",
                column: "RoleDefinitionId",
                principalTable: "RoleDefinitions",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Users_RoleDefinitions_RoleDefinitionId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_RoleDefinitionId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "RoleDefinitionId",
                table: "Users");

            // Note: we do not delete the seeded RoleDefinitions on rollback — they're
            // harmless data and may have been edited by users.
        }
    }
}
