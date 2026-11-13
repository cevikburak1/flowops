using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FlowOps.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "flowops");

            migrationBuilder.CreateTable(
                name: "Organizations",
                schema: "flowops",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Slug = table.Column<string>(type: "text", nullable: false),
                    FinanceThreshold = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Organizations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Memberships",
                schema: "flowops",
                columns: table => new
                {
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Subject = table.Column<string>(type: "text", nullable: false),
                    Role = table.Column<string>(type: "text", nullable: false),
                    Manager = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Memberships", x => new { x.OrganizationId, x.Subject });
                    table.ForeignKey(
                        name: "FK_Memberships_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalSchema: "flowops",
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Purchases",
                schema: "flowops",
                columns: table => new
                {
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Requester = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Category = table.Column<string>(type: "text", nullable: false),
                    Currency = table.Column<string>(type: "text", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    Manager = table.Column<string>(type: "text", nullable: false),
                    Finance = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Purchases", x => new { x.OrganizationId, x.Id });
                    table.ForeignKey(
                        name: "FK_Purchases_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalSchema: "flowops",
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Approvals",
                schema: "flowops",
                columns: table => new
                {
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    PurchaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Stage = table.Column<int>(type: "integer", nullable: false),
                    Assignee = table.Column<string>(type: "text", nullable: false),
                    BookmarkId = table.Column<string>(type: "text", nullable: false),
                    Decision = table.Column<string>(type: "text", nullable: true),
                    Comment = table.Column<string>(type: "text", nullable: true),
                    DecidedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Approvals", x => new { x.OrganizationId, x.PurchaseId, x.Stage });
                    table.ForeignKey(
                        name: "FK_Approvals_Purchases_OrganizationId_PurchaseId",
                        columns: x => new { x.OrganizationId, x.PurchaseId },
                        principalSchema: "flowops",
                        principalTable: "Purchases",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Audit",
                schema: "flowops",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    PurchaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Actor = table.Column<string>(type: "text", nullable: false),
                    Action = table.Column<string>(type: "text", nullable: false),
                    CorrelationId = table.Column<string>(type: "text", nullable: false),
                    At = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Audit", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Audit_Purchases_OrganizationId_PurchaseId",
                        columns: x => new { x.OrganizationId, x.PurchaseId },
                        principalSchema: "flowops",
                        principalTable: "Purchases",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Notifications",
                schema: "flowops",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    PurchaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Recipient = table.Column<string>(type: "text", nullable: false),
                    Kind = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReadAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Notifications_Purchases_OrganizationId_PurchaseId",
                        columns: x => new { x.OrganizationId, x.PurchaseId },
                        principalSchema: "flowops",
                        principalTable: "Purchases",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowCommands",
                schema: "flowops",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    PurchaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Stage = table.Column<int>(type: "integer", nullable: false),
                    NextAttemptAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowCommands", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkflowCommands_Purchases_OrganizationId_PurchaseId",
                        columns: x => new { x.OrganizationId, x.PurchaseId },
                        principalSchema: "flowops",
                        principalTable: "Purchases",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Approvals_OrganizationId_Assignee_Decision",
                schema: "flowops",
                table: "Approvals",
                columns: new[] { "OrganizationId", "Assignee", "Decision" });

            migrationBuilder.CreateIndex(
                name: "IX_Audit_OrganizationId_PurchaseId_At",
                schema: "flowops",
                table: "Audit",
                columns: new[] { "OrganizationId", "PurchaseId", "At" });

            migrationBuilder.CreateIndex(
                name: "IX_Memberships_Subject",
                schema: "flowops",
                table: "Memberships",
                column: "Subject");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_OrganizationId_PurchaseId",
                schema: "flowops",
                table: "Notifications",
                columns: new[] { "OrganizationId", "PurchaseId" });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_OrganizationId_Recipient_CreatedAt",
                schema: "flowops",
                table: "Notifications",
                columns: new[] { "OrganizationId", "Recipient", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Organizations_Slug",
                schema: "flowops",
                table: "Organizations",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Purchases_OrganizationId_Requester_CreatedAt",
                schema: "flowops",
                table: "Purchases",
                columns: new[] { "OrganizationId", "Requester", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowCommands_CompletedAt_NextAttemptAt",
                schema: "flowops",
                table: "WorkflowCommands",
                columns: new[] { "CompletedAt", "NextAttemptAt" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowCommands_OrganizationId_PurchaseId_Stage",
                schema: "flowops",
                table: "WorkflowCommands",
                columns: new[] { "OrganizationId", "PurchaseId", "Stage" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Approvals",
                schema: "flowops");

            migrationBuilder.DropTable(
                name: "Audit",
                schema: "flowops");

            migrationBuilder.DropTable(
                name: "Memberships",
                schema: "flowops");

            migrationBuilder.DropTable(
                name: "Notifications",
                schema: "flowops");

            migrationBuilder.DropTable(
                name: "WorkflowCommands",
                schema: "flowops");

            migrationBuilder.DropTable(
                name: "Purchases",
                schema: "flowops");

            migrationBuilder.DropTable(
                name: "Organizations",
                schema: "flowops");
        }
    }
}
