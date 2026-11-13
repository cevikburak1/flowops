using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FlowOps.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class PurchaseWorkflowGraphs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GraphJson",
                schema: "flowops",
                table: "PurchaseWorkflowVersions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GraphJson",
                schema: "flowops",
                table: "PurchaseWorkflows",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GraphJson",
                schema: "flowops",
                table: "PurchaseWorkflowVersions");

            migrationBuilder.DropColumn(
                name: "GraphJson",
                schema: "flowops",
                table: "PurchaseWorkflows");
        }
    }
}
