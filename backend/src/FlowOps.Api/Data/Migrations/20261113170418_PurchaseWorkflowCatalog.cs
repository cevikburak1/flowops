using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FlowOps.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class PurchaseWorkflowCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "WorkflowId",
                schema: "flowops",
                table: "Purchases",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WorkflowVersion",
                schema: "flowops",
                table: "Purchases",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PurchaseWorkflows",
                schema: "flowops",
                columns: table => new
                {
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    FinanceThreshold = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    PublishedVersion = table.Column<int>(type: "integer", nullable: true),
                    PublishedRevision = table.Column<int>(type: "integer", nullable: true),
                    Archived = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseWorkflows", x => new { x.OrganizationId, x.Id });
                    table.ForeignKey(
                        name: "FK_PurchaseWorkflows_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalSchema: "flowops",
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseWorkflowVersions",
                schema: "flowops",
                columns: table => new
                {
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkflowId = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    FinanceThreshold = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    ElsaVersion = table.Column<int>(type: "integer", nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseWorkflowVersions", x => new { x.OrganizationId, x.WorkflowId, x.Number });
                    table.ForeignKey(
                        name: "FK_PurchaseWorkflowVersions_PurchaseWorkflows_OrganizationId_W~",
                        columns: x => new { x.OrganizationId, x.WorkflowId },
                        principalSchema: "flowops",
                        principalTable: "PurchaseWorkflows",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Purchases_OrganizationId_WorkflowId_CreatedAt",
                schema: "flowops",
                table: "Purchases",
                columns: new[] { "OrganizationId", "WorkflowId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Purchases_OrganizationId_WorkflowId_WorkflowVersion",
                schema: "flowops",
                table: "Purchases",
                columns: new[] { "OrganizationId", "WorkflowId", "WorkflowVersion" });

            migrationBuilder.AddForeignKey(
                name: "FK_Purchases_PurchaseWorkflowVersions_OrganizationId_WorkflowI~",
                schema: "flowops",
                table: "Purchases",
                columns: new[] { "OrganizationId", "WorkflowId", "WorkflowVersion" },
                principalSchema: "flowops",
                principalTable: "PurchaseWorkflowVersions",
                principalColumns: new[] { "OrganizationId", "WorkflowId", "Number" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Purchases_PurchaseWorkflowVersions_OrganizationId_WorkflowI~",
                schema: "flowops",
                table: "Purchases");

            migrationBuilder.DropTable(
                name: "PurchaseWorkflowVersions",
                schema: "flowops");

            migrationBuilder.DropTable(
                name: "PurchaseWorkflows",
                schema: "flowops");

            migrationBuilder.DropIndex(
                name: "IX_Purchases_OrganizationId_WorkflowId_CreatedAt",
                schema: "flowops",
                table: "Purchases");

            migrationBuilder.DropIndex(
                name: "IX_Purchases_OrganizationId_WorkflowId_WorkflowVersion",
                schema: "flowops",
                table: "Purchases");

            migrationBuilder.DropColumn(
                name: "WorkflowId",
                schema: "flowops",
                table: "Purchases");

            migrationBuilder.DropColumn(
                name: "WorkflowVersion",
                schema: "flowops",
                table: "Purchases");
        }
    }
}
