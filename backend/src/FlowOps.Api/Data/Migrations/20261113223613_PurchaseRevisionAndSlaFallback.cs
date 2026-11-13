using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FlowOps.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class PurchaseRevisionAndSlaFallback : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PreviousPurchaseId",
                schema: "flowops",
                table: "Purchases",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EscalationFallback",
                schema: "flowops",
                table: "Approvals",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Purchases_OrganizationId_PreviousPurchaseId",
                schema: "flowops",
                table: "Purchases",
                columns: new[] { "OrganizationId", "PreviousPurchaseId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Purchases_Purchases_OrganizationId_PreviousPurchaseId",
                schema: "flowops",
                table: "Purchases",
                columns: new[] { "OrganizationId", "PreviousPurchaseId" },
                principalSchema: "flowops",
                principalTable: "Purchases",
                principalColumns: new[] { "OrganizationId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Purchases_Purchases_OrganizationId_PreviousPurchaseId",
                schema: "flowops",
                table: "Purchases");

            migrationBuilder.DropIndex(
                name: "IX_Purchases_OrganizationId_PreviousPurchaseId",
                schema: "flowops",
                table: "Purchases");

            migrationBuilder.DropColumn(
                name: "PreviousPurchaseId",
                schema: "flowops",
                table: "Purchases");

            migrationBuilder.DropColumn(
                name: "EscalationFallback",
                schema: "flowops",
                table: "Approvals");
        }
    }
}
