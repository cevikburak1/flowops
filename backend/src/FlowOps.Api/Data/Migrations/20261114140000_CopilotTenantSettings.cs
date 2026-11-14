using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FlowOps.Api.Data.Migrations;

public partial class CopilotTenantSettings : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>("CopilotEnabled", "Organizations", schema: "flowops", nullable: true);
        migrationBuilder.AddColumn<int>("CopilotMonthlyRequests", "Organizations", schema: "flowops", nullable: true);
        migrationBuilder.AddColumn<int>("CopilotHistoryDays", "Organizations", schema: "flowops", nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn("CopilotEnabled", "Organizations", "flowops");
        migrationBuilder.DropColumn("CopilotMonthlyRequests", "Organizations", "flowops");
        migrationBuilder.DropColumn("CopilotHistoryDays", "Organizations", "flowops");
    }
}
