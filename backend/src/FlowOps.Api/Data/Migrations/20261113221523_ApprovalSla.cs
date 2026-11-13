using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FlowOps.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class ApprovalSla : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ApprovalSlaJson",
                schema: "flowops",
                table: "Organizations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ApprovalSlaRevision",
                schema: "flowops",
                table: "Organizations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "WarnAt",
                schema: "flowops",
                table: "Approvals",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "WarnedAt",
                schema: "flowops",
                table: "Approvals",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Approvals_WarnAt",
                schema: "flowops",
                table: "Approvals",
                column: "WarnAt",
                filter: "\"Decision\" IS NULL AND \"WarnedAt\" IS NULL AND \"WarnAt\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Approvals_WarnAt",
                schema: "flowops",
                table: "Approvals");

            migrationBuilder.DropColumn(
                name: "ApprovalSlaJson",
                schema: "flowops",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "ApprovalSlaRevision",
                schema: "flowops",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "WarnAt",
                schema: "flowops",
                table: "Approvals");

            migrationBuilder.DropColumn(
                name: "WarnedAt",
                schema: "flowops",
                table: "Approvals");
        }
    }
}
