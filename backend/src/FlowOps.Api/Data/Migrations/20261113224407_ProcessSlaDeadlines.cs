using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FlowOps.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class ProcessSlaDeadlines : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DueAt",
                schema: "flowops",
                table: "ProcessTasks",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DueAt",
                schema: "flowops",
                table: "ProcessRuns",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SlaSnapshot",
                schema: "flowops",
                table: "ProcessRuns",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DueAt",
                schema: "flowops",
                table: "ProcessTasks");

            migrationBuilder.DropColumn(
                name: "DueAt",
                schema: "flowops",
                table: "ProcessRuns");

            migrationBuilder.DropColumn(
                name: "SlaSnapshot",
                schema: "flowops",
                table: "ProcessRuns");
        }
    }
}
