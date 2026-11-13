using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FlowOps.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class OrganizationTeamsAndAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "DepartmentId",
                schema: "flowops",
                table: "Memberships",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TeamId",
                schema: "flowops",
                table: "Memberships",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "EscalatedAt",
                schema: "flowops",
                table: "Approvals",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ApprovalAssignments",
                schema: "flowops",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    PurchaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Stage = table.Column<int>(type: "integer", nullable: false),
                    Assignee = table.Column<string>(type: "text", nullable: false),
                    Actor = table.Column<string>(type: "text", nullable: false),
                    At = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApprovalAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApprovalAssignments_Approvals_OrganizationId_PurchaseId_Sta~",
                        columns: x => new { x.OrganizationId, x.PurchaseId, x.Stage },
                        principalSchema: "flowops",
                        principalTable: "Approvals",
                        principalColumns: new[] { "OrganizationId", "PurchaseId", "Stage" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrganizationGroups",
                schema: "flowops",
                columns: table => new
                {
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Lead = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizationGroups", x => new { x.OrganizationId, x.Id });
                    table.ForeignKey(
                        name: "FK_OrganizationGroups_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalSchema: "flowops",
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql("""
                INSERT INTO flowops."ApprovalAssignments" ("Id", "OrganizationId", "PurchaseId", "Stage", "Assignee", "Actor", "At")
                SELECT gen_random_uuid(), "OrganizationId", "PurchaseId", "Stage", "Assignee", 'system', "DueAt" - interval '48 hours'
                FROM flowops."Approvals"
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Memberships_OrganizationId_DepartmentId",
                schema: "flowops",
                table: "Memberships",
                columns: new[] { "OrganizationId", "DepartmentId" });

            migrationBuilder.CreateIndex(
                name: "IX_Memberships_OrganizationId_TeamId",
                schema: "flowops",
                table: "Memberships",
                columns: new[] { "OrganizationId", "TeamId" });

            migrationBuilder.CreateIndex(
                name: "IX_Approvals_DueAt",
                schema: "flowops",
                table: "Approvals",
                column: "DueAt",
                filter: "\"Decision\" IS NULL AND \"EscalatedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalAssignments_OrganizationId_PurchaseId_Stage_At",
                schema: "flowops",
                table: "ApprovalAssignments",
                columns: new[] { "OrganizationId", "PurchaseId", "Stage", "At" });

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationGroups_OrganizationId_Kind_Name",
                schema: "flowops",
                table: "OrganizationGroups",
                columns: new[] { "OrganizationId", "Kind", "Name" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Memberships_OrganizationGroups_OrganizationId_DepartmentId",
                schema: "flowops",
                table: "Memberships",
                columns: new[] { "OrganizationId", "DepartmentId" },
                principalSchema: "flowops",
                principalTable: "OrganizationGroups",
                principalColumns: new[] { "OrganizationId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Memberships_OrganizationGroups_OrganizationId_TeamId",
                schema: "flowops",
                table: "Memberships",
                columns: new[] { "OrganizationId", "TeamId" },
                principalSchema: "flowops",
                principalTable: "OrganizationGroups",
                principalColumns: new[] { "OrganizationId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Memberships_OrganizationGroups_OrganizationId_DepartmentId",
                schema: "flowops",
                table: "Memberships");

            migrationBuilder.DropForeignKey(
                name: "FK_Memberships_OrganizationGroups_OrganizationId_TeamId",
                schema: "flowops",
                table: "Memberships");

            migrationBuilder.DropTable(
                name: "ApprovalAssignments",
                schema: "flowops");

            migrationBuilder.DropTable(
                name: "OrganizationGroups",
                schema: "flowops");

            migrationBuilder.DropIndex(
                name: "IX_Memberships_OrganizationId_DepartmentId",
                schema: "flowops",
                table: "Memberships");

            migrationBuilder.DropIndex(
                name: "IX_Memberships_OrganizationId_TeamId",
                schema: "flowops",
                table: "Memberships");

            migrationBuilder.DropIndex(
                name: "IX_Approvals_DueAt",
                schema: "flowops",
                table: "Approvals");

            migrationBuilder.DropColumn(
                name: "DepartmentId",
                schema: "flowops",
                table: "Memberships");

            migrationBuilder.DropColumn(
                name: "TeamId",
                schema: "flowops",
                table: "Memberships");

            migrationBuilder.DropColumn(
                name: "EscalatedAt",
                schema: "flowops",
                table: "Approvals");
        }
    }
}
