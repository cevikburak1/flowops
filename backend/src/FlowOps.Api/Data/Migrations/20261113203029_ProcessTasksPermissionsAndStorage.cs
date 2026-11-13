using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FlowOps.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class ProcessTasksPermissionsAndStorage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<byte[]>(
                name: "Content",
                schema: "flowops",
                table: "ProcessAttachments",
                type: "bytea",
                nullable: true,
                oldClrType: typeof(byte[]),
                oldType: "bytea");

            migrationBuilder.AddColumn<string>(
                name: "ObjectKey",
                schema: "flowops",
                table: "ProcessAttachments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProcessPermissions",
                schema: "flowops",
                table: "Organizations",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ProcessTasks",
                schema: "flowops",
                columns: table => new
                {
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    StepId = table.Column<string>(type: "text", nullable: false),
                    Assignee = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    AssignedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessTasks", x => new { x.OrganizationId, x.RunId, x.StepId });
                    table.ForeignKey(
                        name: "FK_ProcessTasks_ProcessRuns_OrganizationId_RunId",
                        columns: x => new { x.OrganizationId, x.RunId },
                        principalSchema: "flowops",
                        principalTable: "ProcessRuns",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProcessTasks_OrganizationId_Assignee_CompletedAt",
                schema: "flowops",
                table: "ProcessTasks",
                columns: new[] { "OrganizationId", "Assignee", "CompletedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProcessTasks",
                schema: "flowops");

            migrationBuilder.DropColumn(
                name: "ObjectKey",
                schema: "flowops",
                table: "ProcessAttachments");

            migrationBuilder.DropColumn(
                name: "ProcessPermissions",
                schema: "flowops",
                table: "Organizations");

            migrationBuilder.AlterColumn<byte[]>(
                name: "Content",
                schema: "flowops",
                table: "ProcessAttachments",
                type: "bytea",
                nullable: false,
                defaultValue: new byte[0],
                oldClrType: typeof(byte[]),
                oldType: "bytea",
                oldNullable: true);
        }
    }
}
