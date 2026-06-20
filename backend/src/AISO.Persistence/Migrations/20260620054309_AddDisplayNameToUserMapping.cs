using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AISO.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDisplayNameToUserMapping : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DisplayName",
                table: "user_mappings",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SapUserId",
                table: "user_mappings",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DisplayName",
                table: "user_mappings");

            migrationBuilder.DropColumn(
                name: "SapUserId",
                table: "user_mappings");
        }
    }
}
