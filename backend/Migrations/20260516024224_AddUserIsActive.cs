using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tracker.Migrations
{
    /// <inheritdoc />
    public partial class AddUserIsActive : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add the column with defaultValue=true so any user that existed before this
            // migration is treated as active. New rows still get true from the entity
            // default — so behaviour is identical going forward.
            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: true);

            // Belt-and-braces: explicitly set every existing row to active, in case the
            // column was previously added with defaultValue=false on an earlier deploy
            // and someone re-runs this migration against that DB after manual cleanup.
            migrationBuilder.Sql("UPDATE Users SET IsActive = 1 WHERE IsActive = 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Users");
        }
    }
}
