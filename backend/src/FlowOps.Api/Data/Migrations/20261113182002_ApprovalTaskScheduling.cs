using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FlowOps.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class ApprovalTaskScheduling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Priority",
                schema: "flowops",
                table: "Purchases",
                type: "text",
                nullable: false,
                defaultValue: "normal");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DueAt",
                schema: "flowops",
                table: "Approvals",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE flowops."Approvals" AS approval
                SET "DueAt" = COALESCE(
                    (SELECT min(audit."At") FROM flowops."Audit" AS audit
                     WHERE audit."OrganizationId" = approval."OrganizationId"
                       AND audit."PurchaseId" = approval."PurchaseId"
                       AND audit."Action" = CASE approval."Stage" WHEN 1 THEN 'managerReview' ELSE 'financeReview' END),
                    approval."DecidedAt", purchase."CreatedAt") + interval '48 hours'
                FROM flowops."Purchases" AS purchase
                WHERE purchase."OrganizationId" = approval."OrganizationId" AND purchase."Id" = approval."PurchaseId";
                ALTER TABLE flowops."Approvals" ALTER COLUMN "DueAt" SET NOT NULL;
                ALTER TABLE flowops."Purchases" ALTER COLUMN "Priority" DROP DEFAULT;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Approvals_OrganizationId_Assignee_Decision_DueAt",
                schema: "flowops",
                table: "Approvals",
                columns: new[] { "OrganizationId", "Assignee", "Decision", "DueAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Approvals_OrganizationId_Assignee_Decision_DueAt",
                schema: "flowops",
                table: "Approvals");

            migrationBuilder.DropColumn(
                name: "Priority",
                schema: "flowops",
                table: "Purchases");

            migrationBuilder.DropColumn(
                name: "DueAt",
                schema: "flowops",
                table: "Approvals");
        }
    }
}
