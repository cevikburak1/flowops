using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FlowOps.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class ParallelPurchaseApprovals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MinimumApprovals",
                schema: "flowops",
                table: "Purchases",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewersJson",
                schema: "flowops",
                table: "Purchases",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ApprovalVotes",
                schema: "flowops",
                columns: table => new
                {
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    PurchaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Stage = table.Column<int>(type: "integer", nullable: false),
                    Subject = table.Column<string>(type: "text", nullable: false),
                    Decision = table.Column<string>(type: "text", nullable: true),
                    Comment = table.Column<string>(type: "text", nullable: true),
                    DecidedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApprovalVotes", x => new { x.OrganizationId, x.PurchaseId, x.Stage, x.Subject });
                    table.ForeignKey(
                        name: "FK_ApprovalVotes_Approvals_OrganizationId_PurchaseId_Stage",
                        columns: x => new { x.OrganizationId, x.PurchaseId, x.Stage },
                        principalSchema: "flowops",
                        principalTable: "Approvals",
                        principalColumns: new[] { "OrganizationId", "PurchaseId", "Stage" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalVotes_OrganizationId_Subject_Decision",
                schema: "flowops",
                table: "ApprovalVotes",
                columns: new[] { "OrganizationId", "Subject", "Decision" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApprovalVotes",
                schema: "flowops");

            migrationBuilder.DropColumn(
                name: "MinimumApprovals",
                schema: "flowops",
                table: "Purchases");

            migrationBuilder.DropColumn(
                name: "ReviewersJson",
                schema: "flowops",
                table: "Purchases");
        }
    }
}
