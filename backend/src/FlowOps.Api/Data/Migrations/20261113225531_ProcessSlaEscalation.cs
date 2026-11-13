using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FlowOps.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class ProcessSlaEscalation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Notifications_Purchases_OrganizationId_PurchaseId",
                schema: "flowops",
                table: "Notifications");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "EscalatedAt",
                schema: "flowops",
                table: "ProcessTasks",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "WarnAt",
                schema: "flowops",
                table: "ProcessTasks",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "WarnedAt",
                schema: "flowops",
                table: "ProcessTasks",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "EscalatedAt",
                schema: "flowops",
                table: "ProcessRuns",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "PurchaseId",
                schema: "flowops",
                table: "Notifications",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "ProcessId",
                schema: "flowops",
                table: "Notifications",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProcessRunId",
                schema: "flowops",
                table: "Notifications",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProcessTitle",
                schema: "flowops",
                table: "Notifications",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_OrganizationId_ProcessRunId",
                schema: "flowops",
                table: "Notifications",
                columns: new[] { "OrganizationId", "ProcessRunId" });

            migrationBuilder.AddForeignKey(
                name: "FK_Notifications_ProcessRuns_OrganizationId_ProcessRunId",
                schema: "flowops",
                table: "Notifications",
                columns: new[] { "OrganizationId", "ProcessRunId" },
                principalSchema: "flowops",
                principalTable: "ProcessRuns",
                principalColumns: new[] { "OrganizationId", "Id" });

            migrationBuilder.AddForeignKey(
                name: "FK_Notifications_Purchases_OrganizationId_PurchaseId",
                schema: "flowops",
                table: "Notifications",
                columns: new[] { "OrganizationId", "PurchaseId" },
                principalSchema: "flowops",
                principalTable: "Purchases",
                principalColumns: new[] { "OrganizationId", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The previous schema cannot store process notifications or their outbox entries.
            migrationBuilder.Sql("""
                DELETE FROM flowops."Notifications" WHERE "PurchaseId" IS NULL;
                """);
            migrationBuilder.DropForeignKey(
                name: "FK_Notifications_ProcessRuns_OrganizationId_ProcessRunId",
                schema: "flowops",
                table: "Notifications");

            migrationBuilder.DropForeignKey(
                name: "FK_Notifications_Purchases_OrganizationId_PurchaseId",
                schema: "flowops",
                table: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_OrganizationId_ProcessRunId",
                schema: "flowops",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "EscalatedAt",
                schema: "flowops",
                table: "ProcessTasks");

            migrationBuilder.DropColumn(
                name: "WarnAt",
                schema: "flowops",
                table: "ProcessTasks");

            migrationBuilder.DropColumn(
                name: "WarnedAt",
                schema: "flowops",
                table: "ProcessTasks");

            migrationBuilder.DropColumn(
                name: "EscalatedAt",
                schema: "flowops",
                table: "ProcessRuns");

            migrationBuilder.DropColumn(
                name: "ProcessId",
                schema: "flowops",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "ProcessRunId",
                schema: "flowops",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "ProcessTitle",
                schema: "flowops",
                table: "Notifications");

            migrationBuilder.AlterColumn<Guid>(
                name: "PurchaseId",
                schema: "flowops",
                table: "Notifications",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Notifications_Purchases_OrganizationId_PurchaseId",
                schema: "flowops",
                table: "Notifications",
                columns: new[] { "OrganizationId", "PurchaseId" },
                principalSchema: "flowops",
                principalTable: "Purchases",
                principalColumns: new[] { "OrganizationId", "Id" },
                onDelete: ReferentialAction.Cascade);
        }
    }
}
