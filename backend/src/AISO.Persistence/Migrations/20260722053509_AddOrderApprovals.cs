using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AISO.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderApprovals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "order_approvals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SoNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    RequestedBySapUser = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SalesOrg = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    Comment = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    DecidedBySapUser = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    DecisionComment = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    RequestedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DecidedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_order_approvals", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_order_approvals_SoNumber_Status",
                table: "order_approvals",
                columns: new[] { "SoNumber", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_order_approvals_Status_SalesOrg",
                table: "order_approvals",
                columns: new[] { "Status", "SalesOrg" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "order_approvals");
        }
    }
}
