using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AISO.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSapLinkAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sap_link_assignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SapUserId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    TeamsEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    TeamsUserId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Role = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    SalesOrg = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sap_link_assignments", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_user_mappings_SapUserId",
                table: "user_mappings",
                column: "SapUserId",
                unique: true,
                filter: "\"SapUserId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_sap_link_assignments_SapUserId",
                table: "sap_link_assignments",
                column: "SapUserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sap_link_assignments_TeamsEmail",
                table: "sap_link_assignments",
                column: "TeamsEmail",
                unique: true,
                filter: "\"TeamsEmail\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_sap_link_assignments_TeamsUserId",
                table: "sap_link_assignments",
                column: "TeamsUserId",
                unique: true,
                filter: "\"TeamsUserId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sap_link_assignments");

            migrationBuilder.DropIndex(
                name: "IX_user_mappings_SapUserId",
                table: "user_mappings");
        }
    }
}
