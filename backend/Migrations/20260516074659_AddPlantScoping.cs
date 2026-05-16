using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tracker.Migrations
{
    /// <inheritdoc />
    public partial class AddPlantScoping : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop the old per-tenant uniqueness on Shopfloor.Code so it can be replaced
            // with per-plant uniqueness (each plant can have its own SF1).
            migrationBuilder.DropIndex(
                name: "IX_Shopfloors_TenantId_Code",
                table: "Shopfloors");

            // --- STEP 1: Add columns as NULLABLE so the backfill can populate real values
            //             before we tighten to NOT NULL and add the FK constraint.

            migrationBuilder.AddColumn<Guid>(
                name: "PlantId",
                table: "Shopfloors",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PlantId",
                table: "GlassSheets",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PlantId",
                table: "Batches",
                type: "uniqueidentifier",
                nullable: true);

            // Users.PlantId stays nullable forever (null = can access all plants in tenant).
            migrationBuilder.AddColumn<Guid>(
                name: "PlantId",
                table: "Users",
                type: "uniqueidentifier",
                nullable: true);

            // --- STEP 2: Make sure every tenant has at least one Plant. Tenants that already
            //             have one or more plants are left alone (an arbitrary "primary" plant
            //             is picked below for backfill purposes).

            migrationBuilder.Sql(@"
DECLARE @now datetime2 = SYSUTCDATETIME();

INSERT INTO Plants (Id, TenantId, Number, Code, Name, Address, Phone, IsActive, CreatedAtUtc)
SELECT NEWID(), t.Id, 1, 'MAIN', 'Main Plant', NULL, NULL, 1, @now
FROM Tenants t
WHERE NOT EXISTS (SELECT 1 FROM Plants p WHERE p.TenantId = t.Id);
");

            // --- STEP 3: Backfill Shopfloor.PlantId. Use the floor's process->plant link when
            //             available; otherwise fall back to the tenant's lowest-Number plant
            //             (deterministic: the first plant the tenant ever created).

            migrationBuilder.Sql(@"
;WITH primary_plant AS (
    SELECT p.TenantId, p.Id AS PlantId,
           ROW_NUMBER() OVER (PARTITION BY p.TenantId ORDER BY p.Number, p.CreatedAtUtc) AS rn
    FROM Plants p
)
UPDATE sf
SET sf.PlantId = ISNULL(pr.PlantId, pp.PlantId)
FROM Shopfloors sf
LEFT JOIN Processes pr ON pr.Id = sf.ProcessId
LEFT JOIN primary_plant pp ON pp.TenantId = sf.TenantId AND pp.rn = 1
WHERE sf.PlantId IS NULL;
");

            // --- STEP 4: Backfill GlassSheet.PlantId from the sheet's current shopfloor.

            migrationBuilder.Sql(@"
UPDATE g
SET g.PlantId = sf.PlantId
FROM GlassSheets g
INNER JOIN Shopfloors sf ON sf.Id = g.CurrentShopfloorId
WHERE g.PlantId IS NULL;
");

            // --- STEP 5: Backfill Batch.PlantId from the batch's current shopfloor.

            migrationBuilder.Sql(@"
UPDATE b
SET b.PlantId = sf.PlantId
FROM Batches b
INNER JOIN Shopfloors sf ON sf.Id = b.CurrentShopfloorId
WHERE b.PlantId IS NULL;
");

            // --- STEP 6: Tighten Shopfloors.PlantId, GlassSheets.PlantId, Batches.PlantId
            //             to NOT NULL now that every existing row has a value.

            migrationBuilder.AlterColumn<Guid>(
                name: "PlantId",
                table: "Shopfloors",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "PlantId",
                table: "GlassSheets",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "PlantId",
                table: "Batches",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            // --- STEP 7: Indexes + FKs.

            migrationBuilder.CreateIndex(
                name: "IX_Users_PlantId",
                table: "Users",
                column: "PlantId");

            migrationBuilder.CreateIndex(
                name: "IX_Shopfloors_PlantId",
                table: "Shopfloors",
                column: "PlantId");

            migrationBuilder.CreateIndex(
                name: "IX_Shopfloors_PlantId_Code",
                table: "Shopfloors",
                columns: new[] { "PlantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GlassSheets_PlantId",
                table: "GlassSheets",
                column: "PlantId");

            migrationBuilder.CreateIndex(
                name: "IX_Batches_PlantId",
                table: "Batches",
                column: "PlantId");

            migrationBuilder.AddForeignKey(
                name: "FK_Batches_Plants_PlantId",
                table: "Batches",
                column: "PlantId",
                principalTable: "Plants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_GlassSheets_Plants_PlantId",
                table: "GlassSheets",
                column: "PlantId",
                principalTable: "Plants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Shopfloors_Plants_PlantId",
                table: "Shopfloors",
                column: "PlantId",
                principalTable: "Plants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Plants_PlantId",
                table: "Users",
                column: "PlantId",
                principalTable: "Plants",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Batches_Plants_PlantId",
                table: "Batches");

            migrationBuilder.DropForeignKey(
                name: "FK_GlassSheets_Plants_PlantId",
                table: "GlassSheets");

            migrationBuilder.DropForeignKey(
                name: "FK_Shopfloors_Plants_PlantId",
                table: "Shopfloors");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_Plants_PlantId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_PlantId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Shopfloors_PlantId",
                table: "Shopfloors");

            migrationBuilder.DropIndex(
                name: "IX_Shopfloors_PlantId_Code",
                table: "Shopfloors");

            migrationBuilder.DropIndex(
                name: "IX_GlassSheets_PlantId",
                table: "GlassSheets");

            migrationBuilder.DropIndex(
                name: "IX_Batches_PlantId",
                table: "Batches");

            migrationBuilder.DropColumn(
                name: "PlantId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PlantId",
                table: "Shopfloors");

            migrationBuilder.DropColumn(
                name: "PlantId",
                table: "GlassSheets");

            migrationBuilder.DropColumn(
                name: "PlantId",
                table: "Batches");

            migrationBuilder.CreateIndex(
                name: "IX_Shopfloors_TenantId_Code",
                table: "Shopfloors",
                columns: new[] { "TenantId", "Code" },
                unique: true);
        }
    }
}
