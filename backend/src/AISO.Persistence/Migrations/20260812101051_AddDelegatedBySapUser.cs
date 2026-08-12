using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AISO.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDelegatedBySapUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DelegatedBySapUser",
                table: "user_mappings",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DelegatedBySapUser",
                table: "sap_link_assignments",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DelegatedBySapUser",
                table: "user_mappings");

            migrationBuilder.DropColumn(
                name: "DelegatedBySapUser",
                table: "sap_link_assignments");
        }
    }
}
