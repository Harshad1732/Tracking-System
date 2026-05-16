using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tracker.Migrations
{
    /// <inheritdoc />
    public partial class AddUserIsPlatformAdmin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPlatformAdmin",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: false);

            // Promote the seeded ops admin to platform owner. This is the only user that
            // can switch tenants and see every workspace on the platform. New tenant
            // sign-ups never become platform admins automatically.
            migrationBuilder.Sql(
                "UPDATE Users SET IsPlatformAdmin = 1 WHERE Email = 'admin@tracker.local';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsPlatformAdmin",
                table: "Users");
        }
    }
}
