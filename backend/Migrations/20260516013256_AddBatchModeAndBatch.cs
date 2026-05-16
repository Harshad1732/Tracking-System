using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tracker.Migrations
{
    /// <inheritdoc />
    public partial class AddBatchModeAndBatch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BatchMode",
                table: "Shopfloors",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "BatchId",
                table: "GlassSheets",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Batches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BatchNo = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CurrentShopfloorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastMovedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ClosedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Batches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Batches_Shopfloors_CurrentShopfloorId",
                        column: x => x.CurrentShopfloorId,
                        principalTable: "Shopfloors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Batches_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GlassSheets_BatchId",
                table: "GlassSheets",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_Batches_CurrentShopfloorId",
                table: "Batches",
                column: "CurrentShopfloorId");

            migrationBuilder.CreateIndex(
                name: "IX_Batches_TenantId_BatchNo",
                table: "Batches",
                columns: new[] { "TenantId", "BatchNo" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_GlassSheets_Batches_BatchId",
                table: "GlassSheets",
                column: "BatchId",
                principalTable: "Batches",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_GlassSheets_Batches_BatchId",
                table: "GlassSheets");

            migrationBuilder.DropTable(
                name: "Batches");

            migrationBuilder.DropIndex(
                name: "IX_GlassSheets_BatchId",
                table: "GlassSheets");

            migrationBuilder.DropColumn(
                name: "BatchMode",
                table: "Shopfloors");

            migrationBuilder.DropColumn(
                name: "BatchId",
                table: "GlassSheets");
        }
    }
}
