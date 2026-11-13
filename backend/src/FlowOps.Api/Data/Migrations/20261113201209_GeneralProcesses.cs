using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FlowOps.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class GeneralProcesses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Processes",
                schema: "flowops",
                columns: table => new
                {
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Draft = table.Column<string>(type: "text", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    Published = table.Column<string>(type: "text", nullable: true),
                    ElsaDefinitionId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Processes", x => new { x.OrganizationId, x.Id });
                    table.ForeignKey(
                        name: "FK_Processes_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalSchema: "flowops",
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProcessRuns",
                schema: "flowops",
                columns: table => new
                {
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProcessId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    ElsaDefinitionId = table.Column<string>(type: "text", nullable: false),
                    Subject = table.Column<string>(type: "text", nullable: false),
                    Answers = table.Column<string>(type: "text", nullable: false),
                    Fingerprint = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    NextAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessRuns", x => new { x.OrganizationId, x.Id });
                    table.ForeignKey(
                        name: "FK_ProcessRuns_Processes_OrganizationId_ProcessId",
                        columns: x => new { x.OrganizationId, x.ProcessId },
                        principalSchema: "flowops",
                        principalTable: "Processes",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProcessAttachments",
                schema: "flowops",
                columns: table => new
                {
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Content = table.Column<byte[]>(type: "bytea", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessAttachments", x => new { x.OrganizationId, x.RunId });
                    table.ForeignKey(
                        name: "FK_ProcessAttachments_ProcessRuns_OrganizationId_RunId",
                        columns: x => new { x.OrganizationId, x.RunId },
                        principalSchema: "flowops",
                        principalTable: "ProcessRuns",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProcessRuns_NextAt",
                schema: "flowops",
                table: "ProcessRuns",
                column: "NextAt",
                filter: "\"Status\" = 'running'");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessRuns_OrganizationId_ProcessId_CreatedAt",
                schema: "flowops",
                table: "ProcessRuns",
                columns: new[] { "OrganizationId", "ProcessId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProcessAttachments",
                schema: "flowops");

            migrationBuilder.DropTable(
                name: "ProcessRuns",
                schema: "flowops");

            migrationBuilder.DropTable(
                name: "Processes",
                schema: "flowops");
        }
    }
}
