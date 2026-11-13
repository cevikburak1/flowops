using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FlowOps.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class OrganizationReporting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Plan",
                schema: "flowops",
                table: "Organizations",
                type: "text",
                nullable: false,
                defaultValue: "starter");

            migrationBuilder.CreateIndex(
                name: "IX_Purchases_OrganizationId_CreatedAt",
                schema: "flowops",
                table: "Purchases",
                columns: new[] { "OrganizationId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Audit_OrganizationId_At",
                schema: "flowops",
                table: "Audit",
                columns: new[] { "OrganizationId", "At" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Purchases_OrganizationId_CreatedAt",
                schema: "flowops",
                table: "Purchases");

            migrationBuilder.DropIndex(
                name: "IX_Audit_OrganizationId_At",
                schema: "flowops",
                table: "Audit");

            migrationBuilder.DropColumn(
                name: "Plan",
                schema: "flowops",
                table: "Organizations");
        }
    }
}
