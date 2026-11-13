using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FlowOps.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class StudioSubmissionDrafts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StudioSubmissionDrafts",
                schema: "flowops",
                columns: table => new
                {
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProcessId = table.Column<Guid>(type: "uuid", nullable: false),
                    Subject = table.Column<string>(type: "text", nullable: false),
                    ProcessVersion = table.Column<int>(type: "integer", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    Answers = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudioSubmissionDrafts", x => new { x.OrganizationId, x.ProcessId, x.Subject, x.ProcessVersion });
                    table.ForeignKey(
                        name: "FK_StudioSubmissionDrafts_Processes_OrganizationId_ProcessId",
                        columns: x => new { x.OrganizationId, x.ProcessId },
                        principalSchema: "flowops",
                        principalTable: "Processes",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StudioSubmissionDrafts",
                schema: "flowops");
        }
    }
}
