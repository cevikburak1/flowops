using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FlowOps.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class WorkflowSimulation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WorkflowSimulationScenarios",
                schema: "flowops",
                columns: table => new
                {
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkflowId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Input = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowSimulationScenarios", x => new { x.OrganizationId, x.WorkflowId, x.Id });
                    table.ForeignKey(
                        name: "FK_WorkflowSimulationScenarios_PurchaseWorkflows_OrganizationI~",
                        columns: x => new { x.OrganizationId, x.WorkflowId },
                        principalSchema: "flowops",
                        principalTable: "PurchaseWorkflows",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowSimulationRuns",
                schema: "flowops",
                columns: table => new
                {
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkflowId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Actor = table.Column<string>(type: "text", nullable: false),
                    ScenarioId = table.Column<Guid>(type: "uuid", nullable: true),
                    Snapshot = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowSimulationRuns", x => new { x.OrganizationId, x.WorkflowId, x.Id });
                    table.ForeignKey(
                        name: "FK_WorkflowSimulationRuns_PurchaseWorkflows_OrganizationId_Wor~",
                        columns: x => new { x.OrganizationId, x.WorkflowId },
                        principalSchema: "flowops",
                        principalTable: "PurchaseWorkflows",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WorkflowSimulationRuns_WorkflowSimulationScenarios_Organiza~",
                        columns: x => new { x.OrganizationId, x.WorkflowId, x.ScenarioId },
                        principalSchema: "flowops",
                        principalTable: "WorkflowSimulationScenarios",
                        principalColumns: new[] { "OrganizationId", "WorkflowId", "Id" });
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowSimulationRuns_OrganizationId_WorkflowId_CreatedAt",
                schema: "flowops",
                table: "WorkflowSimulationRuns",
                columns: new[] { "OrganizationId", "WorkflowId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowSimulationRuns_OrganizationId_WorkflowId_ScenarioId",
                schema: "flowops",
                table: "WorkflowSimulationRuns",
                columns: new[] { "OrganizationId", "WorkflowId", "ScenarioId" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowSimulationScenarios_OrganizationId_WorkflowId_Creat~",
                schema: "flowops",
                table: "WorkflowSimulationScenarios",
                columns: new[] { "OrganizationId", "WorkflowId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WorkflowSimulationRuns",
                schema: "flowops");

            migrationBuilder.DropTable(
                name: "WorkflowSimulationScenarios",
                schema: "flowops");
        }
    }
}
