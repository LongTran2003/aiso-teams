using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AISO.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRoleAndSalesOrgToUserMapping : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Role",
                table: "user_mappings",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Employee");

            migrationBuilder.AddColumn<string>(
                name: "SalesOrg",
                table: "user_mappings",
                type: "character varying(8)",
                maxLength: 8,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Role",
                table: "user_mappings");

            migrationBuilder.DropColumn(
                name: "SalesOrg",
                table: "user_mappings");
        }
    }
}
