using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tracker.Migrations
{
    /// <inheritdoc />
    public partial class AddNumberColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Number",
                table: "Users",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Number",
                table: "Shopfloors",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Number",
                table: "RoleDefinitions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Number",
                table: "Processes",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Number",
                table: "Plants",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Number",
                table: "GlassSheets",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Number",
                table: "Employees",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Number",
                table: "Customers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Number",
                table: "Batches",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // ---------- BACKFILL: assign sequential per-tenant Number to existing rows ----------
            // For each table, partition by TenantId and number rows by their natural insertion
            // order (CreatedAtUtc / Id). Must run before unique indexes are created — otherwise
            // multiple rows with Number = 0 in the same tenant would collide.
            migrationBuilder.Sql(@"
WITH cte AS (SELECT Id, ROW_NUMBER() OVER (PARTITION BY TenantId ORDER BY CreatedAtUtc, Id) AS rn FROM [Plants])
UPDATE p SET Number = c.rn FROM [Plants] p INNER JOIN cte c ON p.Id = c.Id;");

            migrationBuilder.Sql(@"
WITH cte AS (SELECT Id, ROW_NUMBER() OVER (PARTITION BY TenantId ORDER BY CreatedAtUtc, Id) AS rn FROM [Processes])
UPDATE p SET Number = c.rn FROM [Processes] p INNER JOIN cte c ON p.Id = c.Id;");

            migrationBuilder.Sql(@"
WITH cte AS (SELECT Id, ROW_NUMBER() OVER (PARTITION BY TenantId ORDER BY CreatedAtUtc, Id) AS rn FROM [Employees])
UPDATE e SET Number = c.rn FROM [Employees] e INNER JOIN cte c ON e.Id = c.Id;");

            migrationBuilder.Sql(@"
WITH cte AS (SELECT Id, ROW_NUMBER() OVER (PARTITION BY TenantId ORDER BY CreatedAtUtc, Id) AS rn FROM [Customers])
UPDATE c SET Number = q.rn FROM [Customers] c INNER JOIN cte q ON c.Id = q.Id;");

            migrationBuilder.Sql(@"
WITH cte AS (SELECT Id, ROW_NUMBER() OVER (PARTITION BY TenantId ORDER BY CreatedAtUtc, Id) AS rn FROM [Shopfloors])
UPDATE s SET Number = c.rn FROM [Shopfloors] s INNER JOIN cte c ON s.Id = c.Id;");

            migrationBuilder.Sql(@"
WITH cte AS (SELECT Id, ROW_NUMBER() OVER (PARTITION BY TenantId ORDER BY CreatedAtUtc, Id) AS rn FROM [RoleDefinitions])
UPDATE r SET Number = c.rn FROM [RoleDefinitions] r INNER JOIN cte c ON r.Id = c.Id;");

            migrationBuilder.Sql(@"
WITH cte AS (SELECT Id, ROW_NUMBER() OVER (PARTITION BY TenantId ORDER BY EntryAtUtc, Id) AS rn FROM [GlassSheets])
UPDATE g SET Number = c.rn FROM [GlassSheets] g INNER JOIN cte c ON g.Id = c.Id;");

            migrationBuilder.Sql(@"
WITH cte AS (SELECT Id, ROW_NUMBER() OVER (PARTITION BY TenantId ORDER BY CreatedAtUtc, Id) AS rn FROM [Batches])
UPDATE b SET Number = c.rn FROM [Batches] b INNER JOIN cte c ON b.Id = c.Id;");

            migrationBuilder.Sql(@"
WITH cte AS (SELECT Id, ROW_NUMBER() OVER (PARTITION BY TenantId ORDER BY CreatedAtUtc, Id) AS rn FROM [Users])
UPDATE u SET Number = c.rn FROM [Users] u INNER JOIN cte c ON u.Id = c.Id;");
            // ---------- END BACKFILL ----------

            migrationBuilder.CreateIndex(
                name: "IX_Users_TenantId_Number",
                table: "Users",
                columns: new[] { "TenantId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Shopfloors_TenantId_Number",
                table: "Shopfloors",
                columns: new[] { "TenantId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RoleDefinitions_TenantId_Number",
                table: "RoleDefinitions",
                columns: new[] { "TenantId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Processes_TenantId_Number",
                table: "Processes",
                columns: new[] { "TenantId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Plants_TenantId_Number",
                table: "Plants",
                columns: new[] { "TenantId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GlassSheets_TenantId_Number",
                table: "GlassSheets",
                columns: new[] { "TenantId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Employees_TenantId_Number",
                table: "Employees",
                columns: new[] { "TenantId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Customers_TenantId_Number",
                table: "Customers",
                columns: new[] { "TenantId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Batches_TenantId_Number",
                table: "Batches",
                columns: new[] { "TenantId", "Number" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_TenantId_Number",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Shopfloors_TenantId_Number",
                table: "Shopfloors");

            migrationBuilder.DropIndex(
                name: "IX_RoleDefinitions_TenantId_Number",
                table: "RoleDefinitions");

            migrationBuilder.DropIndex(
                name: "IX_Processes_TenantId_Number",
                table: "Processes");

            migrationBuilder.DropIndex(
                name: "IX_Plants_TenantId_Number",
                table: "Plants");

            migrationBuilder.DropIndex(
                name: "IX_GlassSheets_TenantId_Number",
                table: "GlassSheets");

            migrationBuilder.DropIndex(
                name: "IX_Employees_TenantId_Number",
                table: "Employees");

            migrationBuilder.DropIndex(
                name: "IX_Customers_TenantId_Number",
                table: "Customers");

            migrationBuilder.DropIndex(
                name: "IX_Batches_TenantId_Number",
                table: "Batches");

            migrationBuilder.DropColumn(
                name: "Number",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Number",
                table: "Shopfloors");

            migrationBuilder.DropColumn(
                name: "Number",
                table: "RoleDefinitions");

            migrationBuilder.DropColumn(
                name: "Number",
                table: "Processes");

            migrationBuilder.DropColumn(
                name: "Number",
                table: "Plants");

            migrationBuilder.DropColumn(
                name: "Number",
                table: "GlassSheets");

            migrationBuilder.DropColumn(
                name: "Number",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "Number",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "Number",
                table: "Batches");
        }
    }
}
