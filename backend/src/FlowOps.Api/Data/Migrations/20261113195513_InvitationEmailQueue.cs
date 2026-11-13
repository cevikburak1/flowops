using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FlowOps.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InvitationEmailQueue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EmailAttempts",
                schema: "flowops",
                table: "Invitations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "EmailCode",
                schema: "flowops",
                table: "Invitations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "EmailNextAttemptAt",
                schema: "flowops",
                table: "Invitations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Invitations_EmailNextAttemptAt",
                schema: "flowops",
                table: "Invitations",
                column: "EmailNextAttemptAt",
                filter: "\"EmailCode\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Invitations_EmailNextAttemptAt",
                schema: "flowops",
                table: "Invitations");

            migrationBuilder.DropColumn(
                name: "EmailAttempts",
                schema: "flowops",
                table: "Invitations");

            migrationBuilder.DropColumn(
                name: "EmailCode",
                schema: "flowops",
                table: "Invitations");

            migrationBuilder.DropColumn(
                name: "EmailNextAttemptAt",
                schema: "flowops",
                table: "Invitations");
        }
    }
}
