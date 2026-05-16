using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tracker.Migrations
{
    /// <inheritdoc />
    public partial class AddStatusCatalogAndPlanConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ArrivalStatusCode",
                table: "Shopfloors",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BillingIntervalMonths",
                table: "Plans",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TrialDays",
                table: "Plans",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "SheetStatuses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsInitial = table.Column<bool>(type: "bit", nullable: false),
                    IsTerminal = table.Column<bool>(type: "bit", nullable: false),
                    IsReplaceable = table.Column<bool>(type: "bit", nullable: false),
                    AppliesToSheets = table.Column<bool>(type: "bit", nullable: false),
                    AppliesToBatches = table.Column<bool>(type: "bit", nullable: false),
                    IsSystem = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SheetStatuses", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SheetStatuses_Code",
                table: "SheetStatuses",
                column: "Code",
                unique: true);

            // =====================================================================
            // Seed the 6 default statuses (replaces the AllowedStatuses HashSets
            // that lived in SheetsController + BatchesController).
            // =====================================================================
            migrationBuilder.Sql(@"
                DECLARE @now DATETIME2 = SYSUTCDATETIME();
                INSERT INTO SheetStatuses (
                    Id, Code, Name, SortOrder, IsInitial, IsTerminal, IsReplaceable,
                    AppliesToSheets, AppliesToBatches, IsSystem, IsActive, CreatedAtUtc
                ) VALUES
                (NEWID(), 'Pending',   'Pending',    10, 1, 0, 0, 1, 1, 1, 1, @now),
                (NEWID(), 'InProcess', 'In process', 20, 0, 0, 0, 1, 1, 1, 1, @now),
                (NEWID(), 'Completed', 'Completed',  30, 0, 0, 0, 1, 1, 1, 1, @now),
                (NEWID(), 'Hold',      'On hold',    40, 0, 0, 1, 1, 1, 1, 1, @now),
                (NEWID(), 'Rejected',  'Rejected',   50, 0, 0, 1, 1, 1, 1, 1, @now),
                (NEWID(), 'Delivered', 'Delivered',  60, 0, 1, 0, 1, 1, 1, 1, @now);

                -- Backfill Shopfloor.ArrivalStatusCode (preserves the old
                -- 'IsStorage ? Pending : InProcess' behaviour). Floors can now be
                -- edited to pick any status as their arrival default.
                UPDATE Shopfloors SET ArrivalStatusCode =
                    CASE WHEN IsStorage = 1 THEN 'Pending' ELSE 'InProcess' END
                WHERE ArrivalStatusCode IS NULL;

                -- Plan config defaults: 14-day trial on free plan, monthly billing everywhere.
                UPDATE Plans SET TrialDays = 14 WHERE Code = 'free' AND TrialDays = 0;
                UPDATE Plans SET BillingIntervalMonths = 1 WHERE BillingIntervalMonths = 0;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SheetStatuses");

            migrationBuilder.DropColumn(
                name: "ArrivalStatusCode",
                table: "Shopfloors");

            migrationBuilder.DropColumn(
                name: "BillingIntervalMonths",
                table: "Plans");

            migrationBuilder.DropColumn(
                name: "TrialDays",
                table: "Plans");
        }
    }
}
