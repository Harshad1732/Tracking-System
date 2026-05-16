using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tracker.Migrations
{
    /// <inheritdoc />
    public partial class AddPlanIsDefaultOnSignup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsDefaultOnSignup",
                table: "Plans",
                type: "bit",
                nullable: false,
                defaultValue: false);

            // Backfill: the plan currently used as the sign-up default ("free") gets the flag.
            // Replaces the hardcoded p.Code == "free" lookup in AuthService.RegisterAsync.
            migrationBuilder.Sql(@"UPDATE Plans SET IsDefaultOnSignup = 1 WHERE Code = 'free';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsDefaultOnSignup",
                table: "Plans");
        }
    }
}
