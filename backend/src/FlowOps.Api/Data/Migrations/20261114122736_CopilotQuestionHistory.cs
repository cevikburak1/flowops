using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FlowOps.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class CopilotQuestionHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CopilotQuestions",
                schema: "flowops",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkflowId = table.Column<Guid>(type: "uuid", nullable: false),
                    Subject = table.Column<string>(type: "text", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CopilotQuestions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CopilotQuestions_Memberships_OrganizationId_Subject",
                        columns: x => new { x.OrganizationId, x.Subject },
                        principalSchema: "flowops",
                        principalTable: "Memberships",
                        principalColumns: new[] { "OrganizationId", "Subject" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CopilotQuestions_PurchaseWorkflows_OrganizationId_WorkflowId",
                        columns: x => new { x.OrganizationId, x.WorkflowId },
                        principalSchema: "flowops",
                        principalTable: "PurchaseWorkflows",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CopilotQuestions_ExpiresAt",
                schema: "flowops",
                table: "CopilotQuestions",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_CopilotQuestions_OrganizationId_Subject",
                schema: "flowops",
                table: "CopilotQuestions",
                columns: new[] { "OrganizationId", "Subject" });

            migrationBuilder.CreateIndex(
                name: "IX_CopilotQuestions_OrganizationId_WorkflowId_Subject_CreatedAt",
                schema: "flowops",
                table: "CopilotQuestions",
                columns: new[] { "OrganizationId", "WorkflowId", "Subject", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CopilotQuestions",
                schema: "flowops");
        }
    }
}
