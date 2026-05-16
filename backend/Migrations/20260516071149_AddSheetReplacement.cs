using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tracker.Migrations
{
    /// <inheritdoc />
    public partial class AddSheetReplacement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ReplacementForSheetId",
                table: "GlassSheets",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReplacementReason",
                table: "GlassSheets",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_GlassSheets_ReplacementForSheetId",
                table: "GlassSheets",
                column: "ReplacementForSheetId");

            migrationBuilder.AddForeignKey(
                name: "FK_GlassSheets_GlassSheets_ReplacementForSheetId",
                table: "GlassSheets",
                column: "ReplacementForSheetId",
                principalTable: "GlassSheets",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_GlassSheets_GlassSheets_ReplacementForSheetId",
                table: "GlassSheets");

            migrationBuilder.DropIndex(
                name: "IX_GlassSheets_ReplacementForSheetId",
                table: "GlassSheets");

            migrationBuilder.DropColumn(
                name: "ReplacementForSheetId",
                table: "GlassSheets");

            migrationBuilder.DropColumn(
                name: "ReplacementReason",
                table: "GlassSheets");
        }
    }
}
