using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AISO.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPartialDelegation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "DelegationMaxAmount",
                table: "user_mappings",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DelegationMaxAmount",
                table: "sap_link_assignments",
                type: "numeric",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DelegationMaxAmount",
                table: "user_mappings");

            migrationBuilder.DropColumn(
                name: "DelegationMaxAmount",
                table: "sap_link_assignments");
        }
    }
}
