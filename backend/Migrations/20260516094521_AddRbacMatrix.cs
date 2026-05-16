using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tracker.Migrations
{
    /// <inheritdoc />
    public partial class AddRbacMatrix : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ========================================================================
            // 1. Add new flag columns to RoleDefinitions (alongside the old ones).
            //    We add them as new columns rather than renaming the old bool columns,
            //    because their semantics are different and we need both available at
            //    once during data migration.
            // ========================================================================
            migrationBuilder.AddColumn<bool>(
                name: "IsSystemAdmin",
                table: "RoleDefinitions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsSystem",
                table: "RoleDefinitions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            // ========================================================================
            // 2. Create new RBAC tables.
            // ========================================================================
            migrationBuilder.CreateTable(
                name: "AuthAuditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ActorEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Action = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    TargetType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    TargetId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Details = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuthAuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PermActions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsSystem = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PermActions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PermResources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsSystem = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PermResources", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PlatformAdmins",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GrantedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    GrantedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlatformAdmins", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_PlatformAdmins_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserRoleAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ScopeType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ScopeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRoleAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserRoleAssignments_RoleDefinitions_RoleId",
                        column: x => x.RoleId,
                        principalTable: "RoleDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserRoleAssignments_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserRoleAssignments_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Permissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Permissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Permissions_PermActions_ActionId",
                        column: x => x.ActionId,
                        principalTable: "PermActions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Permissions_PermResources_ResourceId",
                        column: x => x.ResourceId,
                        principalTable: "PermResources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RolePermissions",
                columns: table => new
                {
                    RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PermissionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RolePermissions", x => new { x.RoleId, x.PermissionId });
                    table.ForeignKey(
                        name: "FK_RolePermissions_Permissions_PermissionId",
                        column: x => x.PermissionId,
                        principalTable: "Permissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RolePermissions_RoleDefinitions_RoleId",
                        column: x => x.RoleId,
                        principalTable: "RoleDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex("IX_AuthAuditLogs_AtUtc", "AuthAuditLogs", "AtUtc");
            migrationBuilder.CreateIndex("IX_AuthAuditLogs_TenantId_AtUtc", "AuthAuditLogs", new[] { "TenantId", "AtUtc" });
            migrationBuilder.CreateIndex("IX_PermActions_Code", "PermActions", "Code", unique: true);
            migrationBuilder.CreateIndex("IX_Permissions_ActionId", "Permissions", "ActionId");
            migrationBuilder.CreateIndex("IX_Permissions_ResourceId_ActionId", "Permissions",
                new[] { "ResourceId", "ActionId" }, unique: true);
            migrationBuilder.CreateIndex("IX_PermResources_Code", "PermResources", "Code", unique: true);
            migrationBuilder.CreateIndex("IX_RolePermissions_PermissionId", "RolePermissions", "PermissionId");
            migrationBuilder.CreateIndex("IX_UserRoleAssignments_RoleId", "UserRoleAssignments", "RoleId");
            migrationBuilder.CreateIndex("IX_UserRoleAssignments_TenantId", "UserRoleAssignments", "TenantId");
            migrationBuilder.CreateIndex("IX_UserRoleAssignments_UserId", "UserRoleAssignments", "UserId");
            migrationBuilder.CreateIndex(
                name: "IX_UserRoleAssignments_UserId_RoleId_ScopeType_ScopeId",
                table: "UserRoleAssignments",
                columns: new[] { "UserId", "RoleId", "ScopeType", "ScopeId" },
                unique: true,
                filter: "[ScopeId] IS NOT NULL");

            // ========================================================================
            // 3. Seed Resources, Actions, and the cartesian product into Permissions.
            // ========================================================================
            migrationBuilder.Sql(@"
                DECLARE @now DATETIME2 = SYSUTCDATETIME();

                INSERT INTO PermResources (Id, Code, Name, Description, SortOrder, IsSystem, CreatedAtUtc) VALUES
                (NEWID(), 'Sheets',     'Glass sheets',        'Create, edit and move sheets through shopfloors.', 10,  1, @now),
                (NEWID(), 'Batches',    'Batches',             'Group sheets into batches for production runs.',   20,  1, @now),
                (NEWID(), 'Customers',  'Customers',           'Customer master data.',                            30,  1, @now),
                (NEWID(), 'Employees',  'Employees',           'Employee master data.',                            40,  1, @now),
                (NEWID(), 'Plants',     'Plants',              'Plant master and plant switching.',                50,  1, @now),
                (NEWID(), 'Shopfloors', 'Shopfloors',          'Shopfloor master and sequencing.',                 60,  1, @now),
                (NEWID(), 'Processes',  'Processes',           'Process master data.',                             70,  1, @now),
                (NEWID(), 'Users',      'Users',               'Invite, update and deactivate users.',             80,  1, @now),
                (NEWID(), 'Roles',      'Roles & permissions', 'Create roles and grant permissions.',              90,  1, @now),
                (NEWID(), 'Reports',    'Reports',             'Run and export production reports.',              100,  1, @now),
                (NEWID(), 'Workspace',  'Workspace',           'Workspace name and high-level settings.',         110,  1, @now);

                INSERT INTO PermActions (Id, Code, Name, SortOrder, IsSystem, CreatedAtUtc) VALUES
                (NEWID(), 'View',   'View',   10, 1, @now),
                (NEWID(), 'Add',    'Add',    20, 1, @now),
                (NEWID(), 'Edit',   'Edit',   30, 1, @now),
                (NEWID(), 'Delete', 'Delete', 40, 1, @now);

                INSERT INTO Permissions (Id, ResourceId, ActionId, CreatedAtUtc)
                SELECT NEWID(), r.Id, a.Id, @now
                FROM PermResources r CROSS JOIN PermActions a;
            ");

            // ========================================================================
            // 4. Data migration: convert legacy data to the new model.
            //   - Mark built-in roles (Admin = IsSystemAdmin + IsSystem; others = IsSystem).
            //   - Convert each role's 5 bool flags into RolePermissions rows.
            //   - Insert UserRoleAssignment for every user with RoleDefinitionId.
            //   - Insert PlatformAdmins for every user with IsPlatformAdmin = 1.
            //   - For users whose RoleDefinitionId is null but old Role string was 'Admin',
            //     attach to the tenant's Admin role.
            // ========================================================================
            migrationBuilder.Sql(@"
                UPDATE RoleDefinitions SET IsSystemAdmin = 1, IsSystem = 1 WHERE Name = 'Admin';
                UPDATE RoleDefinitions SET IsSystem = 1 WHERE Name IN ('Manager', 'Operator', 'Viewer');

                DECLARE @now2 DATETIME2 = SYSUTCDATETIME();

                INSERT INTO RolePermissions (RoleId, PermissionId, CreatedAtUtc)
                SELECT r.Id, p.Id, @now2
                FROM RoleDefinitions r
                CROSS JOIN Permissions p
                JOIN PermActions a ON a.Id = p.ActionId
                WHERE r.IsSystemAdmin = 0
                  AND (
                       (a.Code = 'View'   AND r.CanView = 1)
                    OR (a.Code = 'Add'    AND r.CanAdd = 1)
                    OR (a.Code = 'Edit'   AND r.CanEdit = 1)
                    OR (a.Code = 'Delete' AND r.CanDelete = 1)
                  )
                  AND NOT EXISTS (SELECT 1 FROM RolePermissions rp WHERE rp.RoleId = r.Id AND rp.PermissionId = p.Id);

                INSERT INTO RolePermissions (RoleId, PermissionId, CreatedAtUtc)
                SELECT r.Id, p.Id, @now2
                FROM RoleDefinitions r
                CROSS JOIN Permissions p
                JOIN PermResources res ON res.Id = p.ResourceId
                JOIN PermActions a     ON a.Id   = p.ActionId
                WHERE r.IsSystemAdmin = 0
                  AND r.CanViewReports = 1
                  AND res.Code = 'Reports'
                  AND a.Code   = 'View'
                  AND NOT EXISTS (SELECT 1 FROM RolePermissions rp WHERE rp.RoleId = r.Id AND rp.PermissionId = p.Id);

                INSERT INTO UserRoleAssignments (Id, TenantId, UserId, RoleId, ScopeType, ScopeId, CreatedAtUtc, CreatedByUserId)
                SELECT NEWID(), u.TenantId, u.Id, u.RoleDefinitionId,
                       CASE WHEN u.PlantId IS NULL THEN 'Tenant' ELSE 'Plant' END,
                       u.PlantId,
                       @now2,
                       NULL
                FROM Users u
                WHERE u.RoleDefinitionId IS NOT NULL
                  AND NOT EXISTS (
                       SELECT 1 FROM UserRoleAssignments a
                       WHERE a.UserId = u.Id AND a.RoleId = u.RoleDefinitionId
                         AND a.ScopeType = (CASE WHEN u.PlantId IS NULL THEN 'Tenant' ELSE 'Plant' END)
                         AND ((a.ScopeId IS NULL AND u.PlantId IS NULL) OR a.ScopeId = u.PlantId)
                  );

                INSERT INTO UserRoleAssignments (Id, TenantId, UserId, RoleId, ScopeType, ScopeId, CreatedAtUtc, CreatedByUserId)
                SELECT NEWID(), u.TenantId, u.Id, r.Id, 'Tenant', NULL, @now2, NULL
                FROM Users u
                JOIN RoleDefinitions r ON r.TenantId = u.TenantId AND r.Name = 'Admin'
                WHERE u.RoleDefinitionId IS NULL
                  AND u.Role = 'Admin'
                  AND NOT EXISTS (SELECT 1 FROM UserRoleAssignments a WHERE a.UserId = u.Id AND a.RoleId = r.Id AND a.ScopeType = 'Tenant');

                INSERT INTO PlatformAdmins (UserId, GrantedAtUtc, GrantedByUserId)
                SELECT u.Id, @now2, NULL
                FROM Users u
                WHERE u.IsPlatformAdmin = 1
                  AND NOT EXISTS (SELECT 1 FROM PlatformAdmins pa WHERE pa.UserId = u.Id);
            ");

            // ========================================================================
            // 5. Drop legacy columns. Order matters: drop User FK + index first.
            // ========================================================================
            migrationBuilder.DropForeignKey(
                name: "FK_Users_RoleDefinitions_RoleDefinitionId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_RoleDefinitionId",
                table: "Users");

            migrationBuilder.DropColumn(name: "IsPlatformAdmin",  table: "Users");
            migrationBuilder.DropColumn(name: "Role",             table: "Users");
            migrationBuilder.DropColumn(name: "RoleDefinitionId", table: "Users");

            migrationBuilder.DropColumn(name: "CanView",        table: "RoleDefinitions");
            migrationBuilder.DropColumn(name: "CanAdd",         table: "RoleDefinitions");
            migrationBuilder.DropColumn(name: "CanEdit",        table: "RoleDefinitions");
            migrationBuilder.DropColumn(name: "CanDelete",      table: "RoleDefinitions");
            migrationBuilder.DropColumn(name: "CanViewReports", table: "RoleDefinitions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Re-create legacy columns. Best-effort recovery — User.Role string is rebuilt
            // from the user's tenant-scoped role assignment.
            migrationBuilder.AddColumn<string>(
                name: "Role", table: "Users",
                type: "nvarchar(40)", maxLength: 40, nullable: false, defaultValue: "User");
            migrationBuilder.AddColumn<Guid>(
                name: "RoleDefinitionId", table: "Users",
                type: "uniqueidentifier", nullable: true);
            migrationBuilder.AddColumn<bool>(
                name: "IsPlatformAdmin", table: "Users",
                type: "bit", nullable: false, defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "CanView", table: "RoleDefinitions",
                type: "bit", nullable: false, defaultValue: true);
            migrationBuilder.AddColumn<bool>(
                name: "CanAdd", table: "RoleDefinitions",
                type: "bit", nullable: false, defaultValue: false);
            migrationBuilder.AddColumn<bool>(
                name: "CanEdit", table: "RoleDefinitions",
                type: "bit", nullable: false, defaultValue: false);
            migrationBuilder.AddColumn<bool>(
                name: "CanDelete", table: "RoleDefinitions",
                type: "bit", nullable: false, defaultValue: false);
            migrationBuilder.AddColumn<bool>(
                name: "CanViewReports", table: "RoleDefinitions",
                type: "bit", nullable: false, defaultValue: false);

            migrationBuilder.Sql(@"
                UPDATE u
                SET u.Role = r.Name, u.RoleDefinitionId = r.Id
                FROM Users u
                JOIN UserRoleAssignments a ON a.UserId = u.Id AND a.ScopeType = 'Tenant'
                JOIN RoleDefinitions r ON r.Id = a.RoleId;

                UPDATE u SET u.IsPlatformAdmin = 1
                FROM Users u JOIN PlatformAdmins pa ON pa.UserId = u.Id;
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

            migrationBuilder.DropTable(name: "AuthAuditLogs");
            migrationBuilder.DropTable(name: "PlatformAdmins");
            migrationBuilder.DropTable(name: "RolePermissions");
            migrationBuilder.DropTable(name: "UserRoleAssignments");
            migrationBuilder.DropTable(name: "Permissions");
            migrationBuilder.DropTable(name: "PermActions");
            migrationBuilder.DropTable(name: "PermResources");

            migrationBuilder.DropColumn(name: "IsSystem",      table: "RoleDefinitions");
            migrationBuilder.DropColumn(name: "IsSystemAdmin", table: "RoleDefinitions");
        }
    }
}
