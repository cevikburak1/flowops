using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FlowOps.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class ApprovalDelegationSchedules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ApprovalDelegations",
                schema: "flowops",
                columns: table => new
                {
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Subject = table.Column<string>(type: "text", nullable: false),
                    Delegate = table.Column<string>(type: "text", nullable: false),
                    StartsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EndsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApprovalDelegations", x => new { x.OrganizationId, x.Subject });
                    table.ForeignKey(
                        name: "FK_ApprovalDelegations_Memberships_OrganizationId_Delegate",
                        columns: x => new { x.OrganizationId, x.Delegate },
                        principalSchema: "flowops",
                        principalTable: "Memberships",
                        principalColumns: new[] { "OrganizationId", "Subject" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ApprovalDelegations_Memberships_OrganizationId_Subject",
                        columns: x => new { x.OrganizationId, x.Subject },
                        principalSchema: "flowops",
                        principalTable: "Memberships",
                        principalColumns: new[] { "OrganizationId", "Subject" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalDelegations_OrganizationId_Delegate",
                schema: "flowops",
                table: "ApprovalDelegations",
                columns: new[] { "OrganizationId", "Delegate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApprovalDelegations",
                schema: "flowops");
        }
    }
}
